#if __IOS__ || MACCATALYST
using PlatformView = WebKit.WKWebView;
#elif MONOANDROID
using PlatformView = Android.Webkit.WebView;
#elif WINDOWS
using PlatformView = Microsoft.UI.Xaml.Controls.WebView2;
#elif TIZEN
using PlatformView = Tizen.NUI.BaseComponents.View;
#elif (NETSTANDARD || !PLATFORM) || (NET6_0_OR_GREATER && !IOS && !ANDROID && !TIZEN)
using PlatformView = System.Object;
#endif
#if __ANDROID__
using Android.Webkit;
#elif __IOS__
using WebKit;
#endif
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Linq;
using System.Text.Json;

namespace Microsoft.Maui.Handlers
{
	internal interface IHybridWebViewTaskManager
	{
		int GetNextInvokeTaskId();
		ConcurrentDictionary<string, TaskCompletionSource<string>> AsyncTaskCallbacks { get; }
	}

	public partial class HybridWebViewHandler : IHybridWebViewHandler, IHybridWebViewTaskManager
	{
		// IHybridWebViewTaskManager implementation
		ConcurrentDictionary<string, TaskCompletionSource<string>> _asyncTaskCallbacks = new ConcurrentDictionary<string, TaskCompletionSource<string>>();
		int _asyncInvokeTaskId;

		int IHybridWebViewTaskManager.GetNextInvokeTaskId()
		{
			return Interlocked.Increment(ref _asyncInvokeTaskId);
		}
		ConcurrentDictionary<string, TaskCompletionSource<string>> IHybridWebViewTaskManager.AsyncTaskCallbacks => _asyncTaskCallbacks;



		// Using an IP address means that the web view doesn't wait for any DNS resolution,
		// making it substantially faster. Note that this isn't real HTTP traffic, since
		// we intercept all the requests within this origin.
		private static readonly string AppHostAddress = "0.0.0.0";

		private static readonly string AppHostScheme =
#if IOS || MACCATALYST
			"app";
#else
			"https";
#endif

		/// <summary>
		/// Gets the application's base URI. Defaults to <c>https://0.0.0.0/</c> on Windows and Android,
		/// and <c>app://0.0.0.0/</c> on iOS and MacCatalyst (because <c>https</c> is reserved).
		/// </summary>
		internal static readonly string AppOrigin = $"{AppHostScheme}://{AppHostAddress}/";

		internal static readonly Uri AppOriginUri = new(AppOrigin);

		public static IPropertyMapper<IHybridWebView, IHybridWebViewHandler> Mapper = new PropertyMapper<IHybridWebView, IHybridWebViewHandler>(ViewHandler.ViewMapper)
		{
		};

		public static CommandMapper<IHybridWebView, IHybridWebViewHandler> CommandMapper = new(ViewCommandMapper)
		{
			[nameof(IHybridWebView.EvaluateJavaScriptAsync)] = MapEvaluateJavaScriptAsync,
			[nameof(IHybridWebView.InvokeJavaScriptAsync)] = MapInvokeJavaScriptAsync,
			[nameof(IHybridWebView.SendRawMessage)] = MapSendRawMessage,
		};

		public HybridWebViewHandler() : base(Mapper, CommandMapper)
		{
		}

		public HybridWebViewHandler(IPropertyMapper? mapper = null, CommandMapper? commandMapper = null)
			: base(mapper ?? Mapper, commandMapper ?? CommandMapper)
		{
		}

		IHybridWebView IHybridWebViewHandler.VirtualView => VirtualView;

		PlatformView IHybridWebViewHandler.PlatformView => PlatformView;



		/// <summary>
		/// Handler for when the an Async JavaScript task has completed and needs to notify .NET.
		/// </summary>
		private void AsyncTaskCompleted(string taskId, string result)
		{
			// Look for the callback in the list of pending callbacks
			if (!string.IsNullOrEmpty(taskId) && _asyncTaskCallbacks.TryGetValue(taskId, out var callback))
			{
				// Get the callback and remove it from the list
				callback.SetResult(result);

				// Remove the callback
				_asyncTaskCallbacks.TryRemove(taskId, out var _);
			}
		}

		void MessageReceived(string rawMessage)
		{
			if (string.IsNullOrEmpty(rawMessage))
			{
				throw new ArgumentException($"The raw message cannot be null or empty.", nameof(rawMessage));
			}
#if !NETSTANDARD2_0
			var indexOfPipe = rawMessage.IndexOf('|', StringComparison.Ordinal);
#else
			var indexOfPipe = rawMessage.IndexOf("|", StringComparison.Ordinal);
#endif
			if (indexOfPipe == -1)
			{
				throw new ArgumentException($"The raw message must contain a pipe character ('|').", nameof(rawMessage));
			}

			var messageType = rawMessage.Substring(0, indexOfPipe);
			var messageContent = rawMessage.Substring(indexOfPipe + 1);

			switch (messageType)
			{
				case "InvokeMethodCompleted":
					{
						var sections = messageContent.Split('|');
						var taskId = sections[0];
						var result = sections[1];
						AsyncTaskCompleted(taskId, result);
					}
					break;
				case "RawMessage":
					VirtualView?.RawMessageReceived(messageContent);
					break;
				default:
					throw new ArgumentException($"The message type '{messageType}' is not recognized.", nameof(rawMessage));
			}
		}


		public static async void MapInvokeJavaScriptAsync(IHybridWebViewHandler handler, IHybridWebView hybridWebView, object? arg)
		{
#if PLATFORM && !TIZEN
			if (arg is not HybridWebViewInvokeJavaScriptRequest invokeJavaScriptRequest ||
				handler.PlatformView is not MauiHybridWebView hybridPlatformWebView ||
				handler is not IHybridWebViewTaskManager taskManager)
			{
				return;
			}

			// Create a callback for async JavaScript methods to invoke when they are done
			var callback = new TaskCompletionSource<string>();
			var currentInvokeTaskId = $"{taskManager.GetNextInvokeTaskId()}";
			taskManager.AsyncTaskCallbacks.TryAdd(currentInvokeTaskId, callback);

			var paramsValuesStringArray =
				invokeJavaScriptRequest.ParamValues == null
				? string.Empty
				: string.Join(
					", ",
					invokeJavaScriptRequest.ParamValues.Select((v, i) => (v == null ? "null" : JsonSerializer.Serialize(v, invokeJavaScriptRequest.ParamJsonTypeInfos![i]!))));

			await hybridWebView.EvaluateJavaScriptAsync($"window.HybridWebView.InvokeMethod({currentInvokeTaskId}, {invokeJavaScriptRequest.MethodName}, [{paramsValuesStringArray}])");

			var result = await callback.Task;

			invokeJavaScriptRequest.SetResult(result);
#else
			await Task.CompletedTask;
#endif
		}


		internal static async Task<string?> GetAssetContentAsync(string assetPath)
		{
			using var stream = await GetAssetStreamAsync(assetPath);
			if (stream == null)
			{
				return null;
			}
			using var reader = new StreamReader(stream);

			var contents = reader.ReadToEnd();

			return contents;
		}

		internal static async Task<Stream?> GetAssetStreamAsync(string assetPath)
		{
			if (!await FileSystem.AppPackageFileExistsAsync(assetPath))
			{
				return null;
			}
			return await FileSystem.OpenAppPackageFileAsync(assetPath);
		}

#if !NETSTANDARD
		internal static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();
#endif
	}
}
