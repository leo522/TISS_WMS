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
        [HttpGet]
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string userAccount, string pwd)
        {
            string hashedPassword = HashPassword(pwd); //密碼雜湊加密方法要與註冊一致

            // 查詢用戶
            //var user = _db.Users.FirstOrDefault(u => u.UserAccount == userAccount && u.PasswordHash == hashedPassword && u.IsActive);
            var user = _db.Users.FirstOrDefault(u => u.UserAccount == userAccount && u.PasswordHash == hashedPassword && u.IsActive.GetValueOrDefault());

            if (user != null)
            {
                user.LastLogin = DateTime.Now; //成功登入，更新最後登入時間
                _db.SaveChanges();

                Session["UserID"] = user.UserID; //設置用戶到 Session 或 Cookie
                Session["Username"] = user.UserAccount;

                LogUserAction(user.UserID, "登入", Request.UserHostAddress); //記錄登入日誌

                return RedirectToAction("Index", "Home");
            }
            else
            {
                ViewBag.ErrorMessage = "帳號或密碼不正確";
                return View();
            }
        }
        #endregion

        #region 登出
        public ActionResult Logout()
        {
            // 在清除 Session 之前記錄登出日誌
            int userId = Convert.ToInt32(Session["UserID"]);
            LogUserAction(userId, "登出", Request.UserHostAddress);

            // 清除用戶 Session 或 Cookie
            Session.Clear();

            return RedirectToAction("Login");
        }
        #endregion

        #region 註冊帳號
        [HttpGet]
        public ActionResult Register()
        {
            ViewBag.Roles = new SelectList(_db.Roles, "RoleID", "RoleName");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(Users user, string pwd)
        {
            if (ModelState.IsValid)
            {
                // 密碼加密
                user.PasswordHash = HashPassword(pwd);

                // 設置其他初始屬性
                user.CreatedAt = DateTime.Now;
                user.IsActive = true;

                // 自動分配角色，這裡假設 "User" 角色的 RoleID 是 2
                var userRole = _db.Roles.FirstOrDefault(r => r.RoleName == "User");

                _db.Users.Add(user);
                _db.SaveChanges();

                // 日誌記錄
                LogUserAction(user.UserID, "註冊帳號", Request.UserHostAddress);

                return RedirectToAction("Login");
            }

            return View(user);
        }
        #endregion

        #region 密碼加密
        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }
        #endregion

        #region 忘記密碼
        [HttpGet]
        public ActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(string email)
        {
            var user = _db.Users.FirstOrDefault(u => u.Email == email);
            if (user != null)
            {
                string resetLink = Url.Action("ResetPassword", "Account", new { userId = user.UserID }, Request.Url.Scheme);
                string subject = "重置您的密碼";
                string body = $"點擊此連結重置您的密碼: <a href='{resetLink}'>重置密碼</a>";

                SendEmail(user.Email, subject, body);
                ViewBag.Message = "重置密碼的郵件已發送至您的電子郵件。";
            }
            else
            {
                ViewBag.Message = "該郵箱不存在於系統中。";
            }
            return View();
        }
        #endregion

        #region 日誌記錄功能
        private void LogUserAction(int userId, string action, string ipAddress)
        {
            UserLogs log = new UserLogs
            {
                UserID = userId,
                Action = action,
                ActionDate = DateTime.Now,
                IPAddress = ipAddress
            };

            _db.UserLogs.Add(log);
            _db.SaveChanges();
        }
        #endregion

        #region 人員權限管理
        [HttpGet]
        public ActionResult ManageUsers()
        {
            var users = _db.Users.ToList();
            ViewBag.Roles = new SelectList(_db.Roles, "RoleID", "RoleName");  // 傳遞所有角色
            return View(users);
        }

        [HttpPost]
        public ActionResult UpdateRoles(int userId, List<int> roleIds)
        {
            // 找到該用戶的所有現有角色並移除
            var userRoles = _db.UserRoles.Where(ur => ur.UserID == userId).ToList();
            _db.UserRoles.RemoveRange(userRoles);

            // 新增用戶的選定角色
            foreach (var roleId in roleIds)
            {
                _db.UserRoles.Add(new UserRoles
                {
                    UserID = userId,
                    RoleID = roleId
                });
            }

            _db.SaveChanges();

            return RedirectToAction("ManageUsers");
        }


        #endregion

        #region 郵件發送
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

            foreach (var email in toEmail.Split(',')) //分割以逗號分隔的收件人地址並添加到郵件中
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
                Console.WriteLine("郵件發送失敗: " + ex.Message); //處理發送郵件的錯誤
            }
        }
        #endregion
    }
}