using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using TISS_WMS.Models;

namespace TISS_WMS.Controllers
{
    public class MainController : Controller
    {
        private TISS_WMSEntities _db = new TISS_WMSEntities();

        public ActionResult Main()
        {
            return View();
        }
    }
}