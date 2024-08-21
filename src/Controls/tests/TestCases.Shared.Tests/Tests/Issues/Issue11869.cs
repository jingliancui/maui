using NUnit.Framework;
using UITest.Appium;
using UITest.Core;

namespace Microsoft.Maui.TestCases.Tests.Issues;

public class Issue11869 : _IssuesUITest
{
	public Issue11869(TestDevice testDevice) : base(testDevice)
	{
	}

	public override string Issue => "[Bug] ShellContent.IsVisible issue on Android";

	[Test]
	[Category(UITestCategories.Shell)]
	public void IsVisibleWorksForShowingHidingTabs()
	{
		RunningApp.WaitForElement("TopTab2");
		RunningApp.Tap("HideTop2");
		RunningApp.WaitForNoElement("TopTab2");

		RunningApp.WaitForElement("TopTab3");
		RunningApp.Tap("HideTop3");
		RunningApp.WaitForNoElement("TopTab3");

		RunningApp.WaitForElement("Tab 2");
		RunningApp.Tap("HideBottom2");
		RunningApp.WaitForNoElement("Tab 2");

		RunningApp.WaitForElement("Tab 3");
		RunningApp.Tap("HideBottom3");
		RunningApp.WaitForNoElement("Tab 3");

		RunningApp.Tap("ShowAllTabs");
		RunningApp.WaitForElement("TopTab2");
		RunningApp.WaitForElement("TopTab3");
		RunningApp.WaitForElement("Tab 2");
		RunningApp.WaitForElement("Tab 3");
	}
}