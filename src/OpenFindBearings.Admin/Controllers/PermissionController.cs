using Microsoft.AspNetCore.Mvc;

namespace OpenFindBearings.Admin.Controllers;

public class PermissionController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
