using System;
using System.Collections.Generic;
using System.Data.Entity.Validation;
using System.Linq;
using System.Net.Mail;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Mvc;
using static TISS_WMS.Models.accountModel;
using System.Web.Security;
using TISS_WMS.Models;

namespace TISS_WMS.Controllers
{
    public class AccountController : Controller
    {
        private TISS_WMSEntities _db = new TISS_WMSEntities();

        #region 登入
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Login(string UserName, string pwd)
        {
            try
            {
                // 將使用者輸入的密碼進行SHA256加密
                string hashedPwd = ComputeSha256Hash(pwd);
                var dto = _db.Users.FirstOrDefault(u => u.UserName == UserName && u.Password == hashedPwd);

                if (dto != null)
                {
                    // 驗證成功，更新最後登入時間
                    dto.LastLoginDate = DateTime.Now;
                    _db.SaveChanges();

                    // 設定 Session 狀態為已登入
                    Session["LoggedIn"] = true;
                    Session["UserName"] = dto.UserName;

                    // 檢查是否有記錄的返回頁面
                    string returnUrl = Session["ReturnUrl"] != null ? Session["ReturnUrl"].ToString() : Url.Action("Home", "Tiss");

                    // 清除返回頁面的 Session 記錄
                    Session.Remove("ReturnUrl");

                    // 重定向到記錄的返回頁面
                    return Redirect(returnUrl);
                }
                else
                {
                    // 驗證失敗
                    ViewBag.ErrorMessage = "帳號或密碼錯誤";
                    return View();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("其他錯誤: " + ex.Message);
                return View("Error");
            }
        }
        #endregion

        #region 登出
        public ActionResult Logout()
        {
            // 清除所有的 Session 資訊
            //Session.Clear();
            //Session.Abandon();
            Session.Remove("LoggedIn");
            // 清除所有的 Forms 認證 Cookies
            FormsAuthentication.SignOut();

            // 取得登出前的頁面路徑，如果沒有則預設為首頁
            string returnUrl = Request.UrlReferrer != null ? Request.UrlReferrer.ToString() : Url.Action("Home", "Tiss");

            // 重定向到記錄的返回頁面
            return Redirect(returnUrl);
            // 重定向到 Home 頁面
        }
        #endregion

        #region 註冊帳號

        public ActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Register(string EmployeeID, string UserName, string pwd, string Email)
        {
            try
            {
                if (pwd.Length < 6)
                {
                    ViewBag.ErrorMessage = "密碼長度至少要6位數";
                    return View();
                }

                if (_db.Users.Any(u => u.UserName == UserName))
                {
                    ViewBag.ErrorMessage = "該帳號已存在";
                    return View();
                }

                // 驗證員工編號
                var employee = _db.InternalEmployees.FirstOrDefault(e => e.EmployeeID == EmployeeID && e.IsRegistered == false);
                if (employee == null)
                {
                    ViewBag.ErrorMessage = "無效的員工編號或該員工已經註冊";
                    return View();
                }

                //Email格式驗證
                var emailRegex = new System.Text.RegularExpressions.Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
                if (!emailRegex.IsMatch(Email))
                {
                    ViewBag.ErrorMessage = "無效的Email格式";
                    return View();
                }

                // 密碼加密處理
                var Pwd = ComputeSha256Hash(pwd);
                var newUser = new Users
                {
                    UserName = UserName,
                    Password = Pwd,
                    Email = Email,
                    CreatedDate = DateTime.Now,
                    LastLoginDate = DateTime.Now,
                    IsActive = true,
                    //UserAccount = UserName, // 假設 UserAccount 和 UserName 相同
                    ChangeDate = DateTime.Now,
                    IsApproved = false // 預設未開通，註冊後需要管理員審核
                };


                _db.Users.Add(newUser);
                _db.SaveChanges();

                // 發送Email通知管理員
                var adminEmail = "00048@tiss.org.tw";
                var emailBody = $"新使用者註冊，請審核：<br/>帳號: {UserName}<br/>Email: {Email}<br/>註冊時間: {DateTime.Now}<br/><a href='{Url.Action("PendingRegistrations", "Tiss", null, Request.Url.Scheme)}'>點擊這裡審核</a>";
                SendEmail(adminEmail, "新使用者註冊通知", emailBody);

                // 設定訊息給 TempData
                TempData["RegisterMessage"] = "您的帳號已註冊成功，待管理員審核後將發送通知到您的Email。";

                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                Console.WriteLine("其他錯誤: " + ex.Message);
            }
            return View();
        }
        #endregion

        #region 管理員審核
        public ActionResult PendingRegistrations()
        {
            try
            {
                var pendingUsers = _db.Users
                        .Where(u => !(u.IsApproved ?? false))
                        .Select(u => new UsersModel
                        {
                            UserName = u.UserName,
                            Email = u.Email,
                            CreatedDate = DateTime.Now
                        })
                        .ToList();

                return View(pendingUsers);
            }
            catch (Exception ex)
            {
                Console.WriteLine("其他錯誤: " + ex.Message);
                return View("Error");
            }
        }

        [HttpPost]
        public ActionResult ApproveUser(string userName)
        {
            try
            {
                var user = _db.Users.SingleOrDefault(u => u.UserName == userName);

                if (user != null)
                {
                    user.IsApproved = true;

                    // 更新 InternalEmployees 表中的 IsRegistered 欄位
                    var employee = _db.InternalEmployees.FirstOrDefault(e => e.EmailAddress == user.Email);
                    if (employee != null)
                    {
                        employee.IsRegistered = true;
                    }

                    _db.SaveChanges();

                    // 發送Email通知使用者
                    var emailBody = $"您的帳號 {userName} 已通過審核，現在可以登入使用。";
                    SendEmail(user.Email, "國家運動科學中心，網頁管理者帳號審核通過通知", emailBody);
                }

                return RedirectToAction("PendingRegistrations");
            }
            catch (Exception ex)
            {
                Console.WriteLine("其他錯誤: " + ex.Message);
                return View("Error");
            }
        }

        [HttpPost]
        public ActionResult RejectUser(string userName)
        {
            try
            {
                var user = _db.Users.SingleOrDefault(u => u.UserName == userName);
                if (user != null)
                {
                    _db.Users.Remove(user);
                    _db.SaveChanges();
                }
                return RedirectToAction("PendingRegistrations");
            }
            catch (Exception ex)
            {
                Console.WriteLine("其他錯誤: " + ex.Message);
                return View("Error");
            }
        }
        #endregion

        #region 密碼加密
        private static string ComputeSha256Hash(string rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
        #endregion

        #region 忘記密碼
        public ActionResult ForgotPassword()
        {
            return View();
        }

        // 發送重置密碼鏈接
        [HttpPost]
        public ActionResult SendResetLink(string Email)
        {
            try
            {
                var user = _db.Users.FirstOrDefault(u => u.Email == Email);

                if (user == null)
                {
                    ViewBag.ErrorMessage = "此Email尚未註冊";
                    return View("ForgotPassword");
                }

                // 生成重置密碼令牌（這裡使用 Guid 作為示例）
                var resetToken = Guid.NewGuid().ToString();

                // 保存重置令牌和過期時間
                var resetPW = new PasswordResetRequests
                {
                    Email = Email,
                    Token = resetToken,
                    ExpiryDate = DateTime.Now.AddMinutes(5), // 設定有效時間為5分鐘
                    UserAccount = user.UserName,
                    changeDate = DateTime.Now
                };
                _db.PasswordResetRequests.Add(resetPW);
                _db.SaveChanges();

                // 發送重置密碼郵件
                var resetLink = Url.Action("ResetPassword", "Tiss", new { token = resetToken }, Request.Url.Scheme);

                var emailBody = $"請點擊以下連結重置您的密碼：{resetLink}，連結有效時間為5分鐘";

                SendEmail(Email, "重置密碼", emailBody);

                ViewBag.Message = "重置密碼連結已發送至您的郵箱";
                return View("ForgotPassword");
            }
            catch (Exception ex)
            {
                Console.WriteLine("其他錯誤: " + ex.Message);
                return View("Error");
            }
        }

        //郵件發送方法
        private void SendEmail(string toEmail, string subject, string body, string attachmentPath = null)
        {
            var fromEmail = "00048@tiss.org.tw";
            var fromPassword = "lctm hhfh bubx lwda"; //應用程式密碼
            var displayName = "運科中心資訊組"; //顯示的發件人名稱


            var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential(fromEmail, fromPassword),
                EnableSsl = true,
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail, displayName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true,
            };

            // 分割以逗號分隔的收件人地址並添加到郵件中
            foreach (var email in toEmail.Split(','))
            {
                mailMessage.To.Add(email.Trim());
            }

            if (!string.IsNullOrEmpty(attachmentPath))
            {
                Attachment attachment = new Attachment(attachmentPath);
                mailMessage.Attachments.Add(attachment);
            }

            try
            {
                smtpClient.Send(mailMessage);
            }
            catch (Exception ex)
            {
                // 處理發送郵件的錯誤
                Console.WriteLine("郵件發送失敗: " + ex.Message);
            }
        }

        //重置密碼頁面
        public ActionResult ResetPassword(string token)
        {
            try
            {
                // 查找重置請求
                var resetRequest = _db.PasswordResetRequests.SingleOrDefault(r => r.Token == token && r.ExpiryDate > DateTime.Now);

                if (resetRequest == null)
                {
                    ViewBag.ErrorMessage = "無效或過期的要求";
                    return View("Error");
                }

                // 初始化 ResetPasswordViewModel 並傳遞到視圖
                var model = new ResetPasswordViewModel
                {
                    Token = token
                };

                return View(model);
            }
            catch (Exception ex)
            {
                Console.WriteLine("其他錯誤: " + ex.Message);
                return View("Error");
            }

        }

        // 處理重置密碼
        [HttpPost]
        public ActionResult ResetPassword(ResetPasswordViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    // 顯示驗證錯誤
                    var errors = ModelState.Values.SelectMany(v => v.Errors);
                    foreach (var error in errors)
                    {
                        Console.WriteLine(error.ErrorMessage);
                    }
                    return View(model);
                }

                // 根據 Token 查找重置請求
                var resetRequest = _db.PasswordResetRequests
                    .FirstOrDefault(r => r.Token == model.Token && r.ExpiryDate > DateTime.Now);

                if (resetRequest == null)
                {
                    ViewBag.ErrorMessage = "無效或過期的要求";
                    return View("Error");
                }

                // 根據 Email 查找用戶
                var user = _db.Users
                    .FirstOrDefault(u => u.Email == resetRequest.Email);

                if (user == null)
                {
                    ViewBag.ErrorMessage = "無效的帳號";
                    return View("Error");
                }

                // 更新用戶的密碼
                user.Password = ComputeSha256Hash(model.NewPassword);
                //user.changeDate = DateTime.Now;

                // 更新 PasswordResetRequest 表中的 UserAccount 和 ChangeDate
                resetRequest.UserAccount = user.UserName;
                resetRequest.changeDate = DateTime.Now;

                // 刪除重置請求
                _db.PasswordResetRequests.Remove(resetRequest);
            }
            catch (Exception ex)
            {
                Console.WriteLine("其他錯誤: " + ex.Message);
                return View("Error");
            }

            try
            {
                // 儲存變更到資料庫
                _db.SaveChanges();
            }
            catch (DbEntityValidationException ex)
            {
                foreach (var validationErrors in ex.EntityValidationErrors)
                {
                    foreach (var validationError in validationErrors.ValidationErrors)
                    {
                        // 記錄錯誤信息
                        Console.WriteLine($"Property: {validationError.PropertyName}, Error: {validationError.ErrorMessage}");
                    }
                }
                throw;
            }

            ViewBag.Message = "您的密碼已成功重置";
            return RedirectToAction("Login");
        }
        #endregion

        #region 人員權限管理
        public ActionResult UserManager()
        { 
            return View();
        }
        #endregion
    }
}