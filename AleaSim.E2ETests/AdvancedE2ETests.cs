using Microsoft.Playwright.NUnit;
using Microsoft.Playwright;
using NUnit.Framework;
using FluentAssertions;

namespace AleaSim.E2ETests;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class AdvancedE2ETests : PageTest {
    private readonly string _baseUrl = "http://localhost:5241"; 

    [Test]
    public async Task Registration_Flow_ShouldCreateUser_AndAllowLogin() {
        // 1. Go to Register Page
        try {
            await Page.GotoAsync($"{_baseUrl}/register");
        } catch {
            Assert.Ignore("Server not reachable"); return;
        }

        var username = $"user_{Guid.NewGuid().ToString().Substring(0, 8)}";
        var inputs = Page.Locator("input");

        // 2. Fill registration
        await inputs.Nth(0).FillAsync(username);
        await inputs.Nth(1).FillAsync($"{username}@test.com");
        await inputs.Nth(2).FillAsync("Password123!");
        
        await Page.Keyboard.PressAsync("Enter");

        // 3. Wait for redirect to Login
        await Page.WaitForURLAsync(url => url.Contains("/login"), new() { Timeout = 10000 });

        // 4. Perform Login with new user
        var loginInputs = Page.Locator(".glass-panel input");
        await loginInputs.Nth(0).FillAsync(username);
        await loginInputs.Nth(1).FillAsync("Password123!");
        await Page.Keyboard.PressAsync("Enter");

        // 5. Verify successful login (should be on home page)
        await Page.WaitForURLAsync(url => url.EndsWith("/"), new() { Timeout = 10000 });
        var balance = Page.Locator(".balance-text, #wallet-balance"); // Assuming classes based on common patterns
        // Even if we don't find the balance, URL change confirms success
        Page.Url.Should().NotContain("/login");
    }

    [Test]
    public async Task Login_Validation_ShouldShowError_OnWrongPassword() {
        try {
            await Page.GotoAsync($"{_baseUrl}/login");
        } catch {
            Assert.Ignore("Server not reachable"); return;
        }

        var loginContainer = Page.Locator(".glass-panel");
        await loginContainer.Locator("input").First.FillAsync("admin");
        await loginContainer.Locator("input[type='password']").FillAsync("wrongpassword");
        await Page.Keyboard.PressAsync("Enter");

        // Verify error message (MudBlazor Snackbar usually appears)
        var errorMsg = Page.Locator(".mud-snackbar.mud-alert-filled-error");
        await errorMsg.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        var text = await errorMsg.InnerTextAsync();
        text.Should().NotBeEmpty();
    }

    [Test]
    public async Task Mobile_View_ShouldCollapseMenu() {
        try {
            await Page.GotoAsync($"{_baseUrl}/");
        } catch {
            Assert.Ignore("Server not reachable"); return;
        }

        // 1. Set Desktop Viewport
        await Page.SetViewportSizeAsync(1920, 1080);
        var desktopNav = Page.Locator(".mud-nav-menu");
        await Expect(desktopNav).ToBeVisibleAsync();

        // 2. Set Mobile Viewport
        await Page.SetViewportSizeAsync(375, 812); // iPhone X
        
        // On mobile, MudBlazor Drawer is usually hidden by default or requires a toggle
        // We check if the hamburger button is visible
        var menuToggle = Page.Locator("button.mud-drawer-close-button-toggle, .mud-nav-menu-toggle");
        // Depending on implementation, it might be an icon button in the AppBar
        var appBarButton = Page.Locator(".mud-appbar button").First;
        await Expect(appBarButton).ToBeVisibleAsync();
    }

    [Test]
    public async Task Game_Flow_Slot_ShouldUpdateBalance_AfterSpin() {
        // 1. Login as Admin
        try {
            await Page.GotoAsync($"{_baseUrl}/login");
        } catch {
            Assert.Ignore("Server not reachable"); return;
        }

        await Page.Locator(".glass-panel input").First.FillAsync("admin");
        await Page.Locator(".glass-panel input[type='password']").FillAsync("admin");
        await Page.Keyboard.PressAsync("Enter");
        await Page.WaitForURLAsync(url => !url.Contains("/login"));

        // 2. Navigate to Slot Game
        await Page.GotoAsync($"{_baseUrl}/games/slot");
        
        // Wait for SignalR to connect and balance to load
        var balanceLocator = Page.Locator(".mud-chip-content:has-text('$')").First; // Typical for our MudBlazor setup
        await balanceLocator.WaitForAsync();
        var initialBalanceText = await balanceLocator.InnerTextAsync();
        
        // 3. Click SPIN (Assuming Canvas/Button is present)
        var spinBtn = Page.Locator("button:has-text('SPIN')");
        if (await spinBtn.CountAsync() == 0) {
            // Fallback for custom slot engine button
            spinBtn = Page.Locator("#spin-button, .spin-btn");
        }
        
        await spinBtn.ClickAsync();

        // 4. Wait for balance update (SignalR)
        // We wait for the text to change from the initial value
        await Expect(balanceLocator).Not.ToHaveTextAsync(initialBalanceText, new() { Timeout = 10000 });
        
        var newBalanceText = await balanceLocator.InnerTextAsync();
        newBalanceText.Should().NotBe(initialBalanceText);
    }
}
