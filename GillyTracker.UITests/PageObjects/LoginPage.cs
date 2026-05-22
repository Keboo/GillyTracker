namespace GillyTracker.UITests.PageObjects;

/// <summary>
/// Page Object Model for the Login page
/// </summary>
public class LoginPage(IPage page): TestPageBase(page)
{
    // Locators - MUI TextFields need to target the actual input inside the wrapper
    private ILocator EmailInput => Page.GetByTestId("email-input").Locator("input");
    private ILocator PasswordInput => Page.GetByTestId("password-input").Locator("input");
    private ILocator LoginButton => Page.GetByTestId("login-button");
    private ILocator LogoutButton => Page.Locator("button:has-text('Logout')");

    public Task NavigateAsync(Uri baseUrl) => PerformNavigationAsync(baseUrl, "login");

    public async Task LoginAsync(string email, string password)
    {
        await EmailInput.FillAsync(email);
        await PasswordInput.FillAsync(password);
        
        await LoginButton.ClickAsync();
        await Page.WaitForURLAsync("**/", new PageWaitForURLOptions { Timeout = 30000 });
    }
    
    public async Task<bool> IsLoggedInAsync()
    {
        // Check if we're on a page that requires authentication
        // or if we can find user-specific elements
        var url = Page.Url;
        
        if (!url.Contains("/login", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var logoutButtonCount = await LogoutButton.CountAsync();
        return logoutButtonCount > 0;
    }
    
    public async Task LogoutAsync()
    {
        await LogoutButton.ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }
}
