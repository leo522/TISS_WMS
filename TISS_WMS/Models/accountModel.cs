using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TISS_WMS.Models
{
    public class accountModel
    {
        //重置密碼
        public class ResetPasswordViewModel
        {
            public string Token { get; set; }
            public string NewPassword { get; set; }
        }

        public class UsersModel
        {
            public int Id { get; set; }
            public string UserName { get; set; }
            public string Password { get; set; }
            public string Email { get; set; }
            public DateTime CreatedDate { get; set; }
            public DateTime LastLoginDate { get; set; }
            public bool IsActive { get; set; }
            public string UserAccount { get; set; }
            public DateTime? changeDate { get; set; }
        }
    }
}