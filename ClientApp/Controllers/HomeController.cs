using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ClientApp.Models;
using Microsoft.AspNetCore.Authorization;

namespace ClientApp.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }
    [Authorize]
    public IActionResult Privacy()
    {
        return View();
    }
    public IActionResult Logout()
    {
        return SignOut("Cookies", "OpenIdConnect");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
