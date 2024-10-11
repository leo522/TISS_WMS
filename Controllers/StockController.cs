using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using TISS_WMS.Models;
using ZXing;
using ZXing.Common;

namespace TISS_WMS.Controllers
{
    public class StockController : Controller
    {
        private TISS_WMSEntities _db = new TISS_WMSEntities();

        #region 顯示庫存列表
        public ActionResult StockManage()
        {
            var inventoryList = _db.Inventory.ToList();
            return View(inventoryList);
        }
        #endregion

        #region 顯示單一庫存項目的詳細資料
        public ActionResult StockDetails(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Inventory inventory = _db.Inventory.Find(id);
            if (inventory == null)
            {
                return HttpNotFound();
            }
            return View(inventory);
        }
        #endregion

        #region 新增庫存
        public ActionResult StockCreate()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult StockCreate([Bind(Include = "ItemCode,ItemName,Quantity,WarehouseLocationID,MaterialID")] Inventory inventory)
        {
            if (ModelState.IsValid)
            {
                _db.Inventory.Add(inventory);
                _db.SaveChanges();
                return RedirectToAction("StockManage");
            }
            return View(inventory);
        }
        #endregion

        #region 編輯庫存
        public ActionResult StockEdit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Inventory inventory = _db.Inventory.Find(id);
            if (inventory == null)
            {
                return HttpNotFound();
            }
            return View(inventory);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult StockEdit([Bind(Include = "InventoryID,ItemCode,ItemName,Quantity,WarehouseLocationID,MaterialID")] Inventory inventory)
        {
            if (ModelState.IsValid)
            {
                _db.Entry(inventory).State = EntityState.Modified;
                _db.SaveChanges();
                return RedirectToAction("StockManage");
            }
            return View(inventory);
        }
        #endregion

        #region 刪除庫存
        public ActionResult StockDelete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Inventory inventory = _db.Inventory.Find(id);
            if (inventory == null)
            {
                return HttpNotFound();
            }
            return View(inventory);
        }

        [HttpPost, ActionName("StockDelete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Inventory inventory = _db.Inventory.Find(id);
            _db.Inventory.Remove(inventory);
            _db.SaveChanges();
            return RedirectToAction("StockManage");
        }
        #endregion

        #region 釋放資源
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _db.Dispose();
            }
            base.Dispose(disposing);
        }
        #endregion

        #region 條碼生成方法
        public ActionResult GenerateBarcode(string itemCode)
        {
            // 確保物品代碼存在
            var inventoryItem = _db.Inventory.FirstOrDefault(i => i.ItemCode == itemCode);
            if (inventoryItem == null)
            {
                return HttpNotFound("物品代碼不存在");
            }

            // 檢查條碼是否已存在，避免重複生成
            if (!string.IsNullOrEmpty(inventoryItem.Barcode))
            {
                return Content("條碼已經存在");
            }

            // 使用 ZXing.Net 生成條碼
            var barcodeWriter = new BarcodeWriterPixelData
            {
                Format = BarcodeFormat.CODE_128, //條碼格式(可以更換為其他格式)
                Options = new EncodingOptions
                {
                    Height = 150, // 條碼高度
                    Width = 300,  // 條碼寬度
                    Margin = 10   // 邊距
                }
            };

            // 將條碼生成為 PixelData
            var pixelData = barcodeWriter.Write(itemCode);

            // 將 PixelData 轉換為 Bitmap
            using (var bitmap = new Bitmap(pixelData.Width, pixelData.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb))
            {
                for (int y = 0; y < pixelData.Height; y++)
                {
                    for (int x = 0; x < pixelData.Width; x++)
                    {
                        var colorValue = pixelData.Pixels[(y * pixelData.Width + x) * 4];
                        var color = colorValue == 0 ? Color.Black : Color.White;
                        bitmap.SetPixel(x, y, color);
                    }
                }

                // 將 Bitmap 轉換為 MemoryStream 並保存
                using (var memoryStream = new MemoryStream())
                {
                    bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                    byte[] imageBytes = memoryStream.ToArray();

                    // 將條碼以 Base64 存入資料庫
                    string barcodeBase64 = Convert.ToBase64String(imageBytes);
                    inventoryItem.Barcode = barcodeBase64;  // 保存 Base64 字符串
                    _db.SaveChanges();

                    return File(imageBytes, "image/png"); // 返回條碼圖片
                }
            }
        }

        // 顯示條碼
        public ActionResult ShowBarcode(string itemCode)
        {
            var inventoryItem = _db.Inventory.FirstOrDefault(i => i.ItemCode == itemCode);
            if (inventoryItem == null || string.IsNullOrEmpty(inventoryItem.Barcode))
            {
                return HttpNotFound("條碼不存在");
            }

            // 將 Base64 轉換為 byte[] 並返回圖片
            byte[] barcodeBytes = Convert.FromBase64String(inventoryItem.Barcode);
            return File(barcodeBytes, "image/png");
        }
    }
    #endregion
}