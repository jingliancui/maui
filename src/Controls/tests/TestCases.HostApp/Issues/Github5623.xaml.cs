using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls.CustomAttributes;
using Microsoft.Maui.Controls.Internals;
using Microsoft.Maui.Controls.Xaml;
using Microsoft.Maui.Graphics;
using System.Linq;

namespace Maui.Controls.Sample.Issues;

[Preserve(AllMembers = true)]
[Issue(IssueTracker.Github, 5623, "CollectionView with Incremental Collection (RemainingItemsThreshold)", PlatformAffected.All)]
public partial class Github5623 : TestContentPage
{

	int _itemCount = 10;
	const int MaximumItemCount = 100;
	const int PageSize = 10;
	static readonly SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(1, 1);

	public Github5623()
	{
		InitializeComponent();

		BindingContext = new ViewModel5623();
	}

	async void CollectionView_RemainingItemsThresholdReached(object sender, System.EventArgs e)
	{
		await SemaphoreSlim.WaitAsync();
		try
		{
			var itemsSource = (sender as CollectionView).ItemsSource as ObservableCollection<Model5623>;
			var nextSet = await GetNextSetAsync();

			// nothing to add
			if (nextSet.Count == 0)
				return;

#pragma warning disable CS0618 // Type or member is obsolete
#pragma warning disable CS0612 // Type or member is obsolete
			Device.BeginInvokeOnMainThread(() =>
			{
				foreach (var item in nextSet)
				{
					itemsSource.Add(item);
				}
			});
#pragma warning restore CS0612 // Type or member is obsolete
#pragma warning restore CS0618 // Type or member is obsolete

			System.Diagnostics.Debug.WriteLine("Count: " + itemsSource.Count);
		}
		finally
		{
			SemaphoreSlim.Release();
		}
	}

	void CollectionView_OnScrolled(object sender, ItemsViewScrolledEventArgs e)
	{
		Label1.Text = "HorizontalDelta: " + e.HorizontalDelta;
		Label2.Text = "VerticalDelta: " + e.VerticalDelta;
		Label3.Text = "HorizontalOffset: " + e.HorizontalOffset;
		Label4.Text = "VerticalOffset: " + e.VerticalOffset;
		Label5.Text = "FirstVisibleItemIndex: " + e.FirstVisibleItemIndex;
		Label6.Text = "CenterItemIndex: " + e.CenterItemIndex;
		Label7.Text = "LastVisibleItemIndex: " + e.LastVisibleItemIndex;
	}

	async Task<ObservableCollection<Model5623>> GetNextSetAsync()
	{
		return await Task.Run(() =>
		{
			var collection = new ObservableCollection<Model5623>();
			var count = PageSize;

			if (_itemCount + count > MaximumItemCount)
				count = MaximumItemCount - _itemCount;

			for (var i = _itemCount; i < _itemCount + count; i++)
			{
				collection.Add(new Model5623((BindingContext as ViewModel5623).ItemSizingStrategy == ItemSizingStrategy.MeasureAllItems)
				{
					Text = i.ToString(),
					BackgroundColor = i % 2 == 0 ? Colors.AntiqueWhite : Colors.Lavender
				});
			}

			_itemCount += count;

			return collection;
		});
	}


	protected override void Init()
	{

	}
}

[Preserve(AllMembers = true)]
public class ViewModel5623
{
	public ObservableCollection<Model5623> Items { get; set; }

	public Command RemainingItemsThresholdReachedCommand { get; set; }

	public ItemSizingStrategy ItemSizingStrategy { get; set; } = ItemSizingStrategy.MeasureAllItems;

	public ViewModel5623()
	{
		var collection = new ObservableCollection<Model5623>();
		var pageSize = 10;

		for (var i = 0; i < pageSize; i++)
		{
			collection.Add(new Model5623(ItemSizingStrategy == ItemSizingStrategy.MeasureAllItems)
			{
				Text = i.ToString(),
				BackgroundColor = i % 2 == 0 ? Colors.AntiqueWhite : Colors.Lavender
			});
		}

		Items = collection;

		RemainingItemsThresholdReachedCommand = new Command(() =>
		{
			System.Diagnostics.Debug.WriteLine($"{nameof(RemainingItemsThresholdReachedCommand)} called");
		});
	}
}

[Preserve(AllMembers = true)]
public class Model5623
{
	Random random = new Random();

	public string Text { get; set; }

	public Color BackgroundColor { get; set; }

	public int Height { get; set; } = 200;

	public string HeightText { get; private set; }

	public Model5623(bool isUneven)
	{
		var byteArray = new byte[4];
		random.NextBytes(byteArray);

		if (isUneven)
			Height = 100 + (BitConverter.ToInt32(byteArray, 0) % 300 + 300) % 300;

		HeightText = "(Height: " + Height + ")";
	}
}
