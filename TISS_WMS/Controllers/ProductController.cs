using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using TISS_WMS.Models;

namespace TISS_WMS.Controllers
{
    public class ProductController : Controller
    {
        private TISS_WMSEntities _db = new TISS_WMSEntities();

        public ActionResult AddProduct()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AddProduct(Product product, int supplierId, int quantity) 
        {
            if (ModelState.IsValid)
            {
                //創建入庫單據
                var receipt = new Receipt
                {
                    //SupplierID = supplierId,
                    ReceiptDate = DateTime.Now,
                };

                _db.Receipt.Add(receipt);

                await _db.SaveChangesAsync();

                //創建入庫明細
                var receiptDetails = new ReceiptDetails
                {
                    ReceiptID = receipt.ReceiptID,
                    //ProductID = product.ProductID,
                    Quantity = quantity
                };

                _db.ReceiptDetails.Add(receiptDetails);

                //更新產品庫存
                product.Stock += quantity;

                await _db.SaveChangesAsync();

                return RedirectToAction("Main");
            }
            return View(product);
        }
    }
}