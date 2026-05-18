using GillyTracker.UITests.PageObjects;

namespace GillyTracker.UITests;

/// <summary>
/// Placeholder test class - actual tests are in QAWorkflowTests.cs
/// This file can be used for additional test scenarios
/// </summary>
public class HomePageTests : UITestBase
{
    [Test]
    public async Task CanNavigateToHomePage()
    {
        HomePage homePage = new(Page);
        await homePage.NavigateAsync(FrontendBaseUri);

        await homePage.AssertIsLoadedAsync();
        await homePage.AssertFormIsVisibleAsync();
    }

    [Test]
    public async Task CanFillManualLocationDetails()
    {
        HomePage homePage = new(Page);
        await homePage.NavigateAsync(FrontendBaseUri);

        const string latitude = "47.6205";
        const string longitude = "-122.3493";
        const string details = "Seen near the park entrance.";

        await homePage.FillReportFormAsync(latitude, longitude, details);
        await homePage.AssertFormValuesAsync(latitude, longitude, details);
    }

    [Test]
    [Category(TestCategories.Accessibility)]
    public async Task HomePageIsAccessible()
    {
        HomePage homePage = new(Page);
        await homePage.NavigateAsync(FrontendBaseUri);

        await AssertNoAccessibilityViolations();
    }
}
