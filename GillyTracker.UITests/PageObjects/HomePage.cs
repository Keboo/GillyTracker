using System.Text.RegularExpressions;

namespace GillyTracker.UITests.PageObjects;

public class HomePage(IPage page) : TestPageBase(page)
{
    private ILocator Heading => Page.GetByRole(AriaRole.Heading, new() { NameRegex = new Regex(@"Report Gilly.?s Location") });
    private ILocator LocationMap => Page.Locator(".location-map");
    private ILocator CoordinatePills => Page.Locator(".coordinate-pill");
    private ILocator DetailsInput => Page.GetByLabel("Contact details or notes");
    private ILocator SubmitButton => Page.GetByRole(AriaRole.Button, new() { Name = "Send report" });

    public Task NavigateAsync(Uri baseUrl) => PerformNavigationAsync(baseUrl, "");

    public async Task AssertIsLoadedAsync()
    {
        await Heading.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await SubmitButton.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
    }

    public async Task FillReportFormAsync(string details)
    {
        await DetailsInput.FillAsync(details);
    }
    
    public async Task AssertFormIsVisibleAsync()
    {
        await LocationMap.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await CoordinatePills.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await DetailsInput.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
    }

    public async Task AssertFormValuesAsync(string details)
    {
        await Assertions.Expect(CoordinatePills).ToHaveCountAsync(2);
        await Assertions.Expect(DetailsInput).ToHaveValueAsync(details);
    }
}
