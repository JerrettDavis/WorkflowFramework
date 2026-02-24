using FluentAssertions;
using Microsoft.Playwright;
using Reqnroll;
using WorkflowFramework.Dashboard.UITests.Hooks;

namespace WorkflowFramework.Dashboard.UITests.StepDefinitions;

[Binding]
public sealed class AuthSteps
{
    private readonly ScenarioContext _context;

    public AuthSteps(ScenarioContext context)
    {
        _context = context;
    }

    private IPage Page => _context.Get<IPage>();
    private string WebUrl => AspireHooks.Fixture.WebBaseUrl;

    [When("I navigate to the login page")]
    public async Task WhenINavigateToTheLoginPage()
    {
        await Page.GotoAsync($"{WebUrl}/login",
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.WaitForTimeoutAsync(1000);
    }

    [When("I navigate to the register page")]
    public async Task WhenINavigateToTheRegisterPage()
    {
        await Page.GotoAsync($"{WebUrl}/register",
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.WaitForTimeoutAsync(1000);
    }

    [Then("I should see the login form")]
    public async Task ThenIShouldSeeTheLoginForm()
    {
        await Page.WaitForSelectorAsync("[data-testid='login-username']",
            new PageWaitForSelectorOptions { Timeout = 10_000 });
        var loginUsername = Page.Locator("[data-testid='login-username']");
        (await loginUsername.IsVisibleAsync()).Should().BeTrue("Login form should be visible");
    }

    [Then("the login form should have username and password fields")]
    public async Task ThenTheLoginFormShouldHaveUsernameAndPasswordFields()
    {
        var username = Page.Locator("[data-testid='login-username']");
        var password = Page.Locator("[data-testid='login-password']");
        var submit = Page.Locator("[data-testid='login-submit']");

        (await username.IsVisibleAsync()).Should().BeTrue("Username field should be visible");
        (await password.IsVisibleAsync()).Should().BeTrue("Password field should be visible");
        (await submit.IsVisibleAsync()).Should().BeTrue("Submit button should be visible");
    }

    [Then("I should see the registration form")]
    public async Task ThenIShouldSeeTheRegistrationForm()
    {
        var registerUsername = Page.Locator("[data-testid='register-username']");
        var registerEmail = Page.Locator("[data-testid='register-email']");
        var registerPassword = Page.Locator("[data-testid='register-password']");

        (await registerUsername.IsVisibleAsync()).Should().BeTrue("Register username field should be visible");
        (await registerEmail.IsVisibleAsync()).Should().BeTrue("Register email field should be visible");
        (await registerPassword.IsVisibleAsync()).Should().BeTrue("Register password field should be visible");
    }

    [Then("I should see a login link in the navigation")]
    public async Task ThenIShouldSeeALoginLinkInTheNavigation()
    {
        var loginLink = Page.Locator("[data-testid='nav-login']");
        (await loginLink.IsVisibleAsync()).Should().BeTrue("Login link should be visible in navigation");
    }

    [Then("the designer should be fully functional")]
    public async Task ThenTheDesignerShouldBeFullyFunctional()
    {
        var canvas = Page.Locator("#workflow-canvas");
        (await canvas.IsVisibleAsync()).Should().BeTrue("Canvas should be visible in anonymous mode");

        var toolbar = Page.Locator("[data-testid='toolbar']");
        (await toolbar.IsVisibleAsync()).Should().BeTrue("Toolbar should be visible in anonymous mode");
    }

    [Then("I should be able to interact with the canvas")]
    public async Task ThenIShouldBeAbleToInteractWithTheCanvas()
    {
        // Verify key interactive elements are present
        var saveBtn = Page.Locator("[data-testid='btn-save']");
        (await saveBtn.IsVisibleAsync()).Should().BeTrue("Save button should be accessible in anonymous mode");

        var newBtn = Page.Locator("[data-testid='btn-new']");
        (await newBtn.IsVisibleAsync()).Should().BeTrue("New button should be accessible in anonymous mode");
    }
}
