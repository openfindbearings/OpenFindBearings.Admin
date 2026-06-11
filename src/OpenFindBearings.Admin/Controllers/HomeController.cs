using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Admin.Models;
using OpenFindBearings.Admin.Services;

namespace OpenFindBearings.Admin.Controllers;

public class HomeController : Controller
{
    private readonly ServiceHealthService _health;

    public HomeController(ServiceHealthService health)
    {
        _health = health;
    }

    public IActionResult Index()
    {
        return View();
    }

    public async Task<IActionResult> Status()
    {
        var result = await _health.CheckAllAsync();
        return Json(result);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
