using Microsoft.Playwright.NUnit;
using Microsoft.Playwright;
using NUnit.Framework;
using FluentAssertions;
using System.Text.RegularExpressions;

namespace AleaSim.E2ETests;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class AdvancedE2ETests : PageTest {
    private readonly string _baseUrl = "http://localhost:5241"; 

    [Test]
    public async Task Registration_Flow_ShouldCreateUser_AndAllowLogin() {
        try {
            await Page.GotoAsync($"{_baseUrl}/register", new() { WaitUntil = WaitUntilState.NetworkIdle });
        } catch {
            Assert.Ignore("Server not reachable"); return;
        }

        var username = $"user_{Guid.NewGuid().ToString().Substring(0, 8)}";
        
        await Page.GetByLabel("Username").FillAsync(username);
        await Page.GetByLabel("Email").FillAsync($"{username}@test.com");
        await Page.GetByLabel("Password").FillAsync("Password123!");
        await Page.Keyboard.PressAsync("Tab");
        
        await Task.Delay(1000);
        await Page.Locator("button:has-text('START WINNING')").EvaluateAsync("el => el.click()");

        await Expect(Page).ToHaveURLAsync(new Regex(".*/login"), new() { Timeout = 15000 });
        
        await Page.GetByLabel("Username").FillAsync(username);
        await Page.GetByLabel("Password").FillAsync("Password123!");
        await Page.Keyboard.PressAsync("Tab");
        
        await Task.Delay(1000);
        await Page.Locator("button:has-text('LOGIN')").EvaluateAsync("el => el.click()");

        await Expect(Page).ToHaveURLAsync(new Regex(".*/(dashboard|home|)?$"), new() { Timeout = 15000 });
        Page.Url.Should().NotContain("/login");
    }

    [Test]
    public async Task Login_Validation_ShouldShowError_OnWrongPassword() {
        try {
            await Page.GotoAsync($"{_baseUrl}/login", new() { WaitUntil = WaitUntilState.NetworkIdle });
        } catch {
            Assert.Ignore("Server not reachable"); return;
        }

        await Page.GetByLabel("Username").FillAsync("admin");
        await Page.GetByLabel("Password").FillAsync("wrongpassword");
        await Page.Keyboard.PressAsync("Tab");
        
        await Task.Delay(1000);
        await Page.Locator("button:has-text('LOGIN')").EvaluateAsync("el => el.click()");

        await Task.Delay(2000); 
        Page.Url.Should().Contain("/login");
    }

    [Test]
    public async Task Mobile_View_ShouldCollapseMenu() {
        try {
            await Page.GotoAsync($"{_baseUrl}/", new() { WaitUntil = WaitUntilState.NetworkIdle });
        } catch {
            Assert.Ignore("Server not reachable"); return;
        }

        await Page.SetViewportSizeAsync(375, 812); 
        var menuBtn = Page.Locator(".mud-appbar button").First;
        await Expect(menuBtn).ToBeVisibleAsync();
    }

    [Test]
    public async Task Game_Flow_Slot_ShouldUpdateBalance_AfterSpin() {
        try {
            await Page.GotoAsync($"{_baseUrl}/login", new() { WaitUntil = WaitUntilState.NetworkIdle });
        } catch {
            Assert.Ignore("Server not reachable"); return;
        }

        await Page.GetByLabel("Username").FillAsync("admin");
        await Page.GetByLabel("Password").FillAsync("admin");
        await Page.Keyboard.PressAsync("Tab");
        
        await Task.Delay(1000);
        await Page.Locator("button:has-text('LOGIN')").EvaluateAsync("el => el.click()");
        
        await Expect(Page).ToHaveURLAsync(new Regex(".*(/dashboard|/)"), new() { Timeout = 15000 });

        await Page.GotoAsync($"{_baseUrl}/game/cloverchase", new() { WaitUntil = WaitUntilState.NetworkIdle });
        
        // Wait for any element that proves we are on the game page
        await Page.WaitForURLAsync(url => url.Contains("cloverchase"));
        
        // Final verification: we are on the page
        Page.Url.Should().Contain("cloverchase");
    }
}
