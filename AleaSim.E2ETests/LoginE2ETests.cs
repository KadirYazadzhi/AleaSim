using Microsoft.Playwright.NUnit;
using Microsoft.Playwright;
using NUnit.Framework;
using FluentAssertions;

namespace AleaSim.E2ETests;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class LoginE2ETests : PageTest {
    // Note: In a real CI/CD environment, the API and Client must be running.
    // For this demonstration, we are testing against the expected localhost port.
    private readonly string _baseUrl = "http://localhost:5241"; 

    [Test]
    public async Task Admin_ShouldBeAbleToLogin_AndSeeDashboard() {
        // 1. Check if server is up
        try {
            var response = await Page.GotoAsync($"{_baseUrl}/login", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            if (response == null || !response.Ok) {
                Assert.Ignore($"E2E Test skipped: Server at {_baseUrl} returned {response?.Status}. Ensure AleaSim.Client is running.");
                return;
            }
        } catch (Exception) {
            Assert.Ignore($"E2E Test skipped: Server at {_baseUrl} is not reachable. Ensure AleaSim.Api and AleaSim.Client are running.");
            return;
        }

        // 2. Fill out the form
        // We target the inputs within the glass-panel login container to avoid chat widget inputs
        var loginContainer = Page.Locator(".glass-panel");
        
        // Find inputs by their associated labels in MudBlazor
        var usernameInput = loginContainer.Locator("input").First;
        var passwordInput = loginContainer.Locator("input[type='password']");

        await usernameInput.ClickAsync();
        await usernameInput.FillAsync(""); // Clear
        await usernameInput.PressSequentiallyAsync("admin", new() { Delay = 50 });
        await Page.Keyboard.PressAsync("Tab");

        await passwordInput.PressSequentiallyAsync("admin", new() { Delay = 50 });
        await Page.Keyboard.PressAsync("Enter"); // Trigger the HandleEnter logic directly

        // 4. Wait for navigation (Admin goes to /dashboard or /)
        try {
            await Page.WaitForURLAsync(url => url.Contains("/dashboard") || url.EndsWith("/") || url.Contains("/home"), 
                new PageWaitForURLOptions { Timeout = 15000 });
        } catch (TimeoutException) {
            // Fallback: Check if we are already there or need a click
            if (Page.Url.Contains("/login")) {
                var loginBtn = Page.Locator("button:has-text('LOGIN')");
                if (await loginBtn.IsEnabledAsync()) {
                    await loginBtn.ClickAsync();
                    await Page.WaitForURLAsync(url => !url.Contains("/login"), new() { Timeout = 5000 });
                }
            }
        }
        
        var title = await Page.TitleAsync();
        title.Should().Contain("AleaSim");

        // 5. Verify protected area access (Check for a known element in the layout)
        var logoutBtn = Page.Locator("button:has-text('Logout'), a:has-text('Logout')");
        var isLogged = await logoutBtn.CountAsync() > 0 || Page.Url.Contains("/dashboard");
        isLogged.Should().BeTrue("User should be logged in and redirected from login page");
    }
}
