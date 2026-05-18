using System.Text.RegularExpressions;

namespace GillyTracker.UITests.PageObjects;

public class HomePage(IPage page) : TestPageBase(page)
{
    private ILocator Heading => Page.GetByRole(AriaRole.Heading, new() { NameRegex = new Regex(@"Report Gilly.?s Location") });
    private ILocator LatitudeInput => Page.GetByLabel("Latitude");
    private ILocator LongitudeInput => Page.GetByLabel("Longitude");
    private ILocator DetailsInput => Page.GetByLabel("Contact details or notes");
    private ILocator SubmitButton => Page.GetByRole(AriaRole.Button, new() { Name = "Send report" });

    public Task NavigateAsync(Uri baseUrl) => PerformNavigationAsync(baseUrl, "");

    public async Task AssertIsLoadedAsync()
    {
        await Heading.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await SubmitButton.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
    }

    public async Task FillReportFormAsync(string latitude, string longitude, string details)
    {
        await LatitudeInput.FillAsync(latitude);
        await LongitudeInput.FillAsync(longitude);
        await DetailsInput.FillAsync(details);
    }
    
    public async Task AssertFormIsVisibleAsync()
    {
        await LatitudeInput.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await LongitudeInput.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await DetailsInput.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
    }

    public async Task AssertFormValuesAsync(string latitude, string longitude, string details)
    {
        await Assertions.Expect(LatitudeInput).ToHaveValueAsync(latitude);
        await Assertions.Expect(LongitudeInput).ToHaveValueAsync(longitude);
        await Assertions.Expect(DetailsInput).ToHaveValueAsync(details);
    }
}
