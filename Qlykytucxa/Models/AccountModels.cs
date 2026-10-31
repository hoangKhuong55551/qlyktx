using System;
using System.ComponentModel.DataAnnotations;
using System.Web;

namespace Qlykytucxa.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên tài khoản hoặc mã sinh viên!")]
        [Display(Name = "Tài khoản")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu!")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; }

        [Display(Name = "Ghi nhớ đăng nhập")]
        public bool RememberMe { get; set; }
    }

    // Ten cac vai tro trong he thong
    public static class UserRoles
    {
        public const string Admin = "Admin";
        public const string Staff = "Staff";
        public const string Student = "Student";
    }

    public class UserSession
    {
        public string Username { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; } // "Admin", "Staff", "Student"
        public int? StudentId { get; set; }
        public int? RoomId { get; set; }
        public string RoomNumber { get; set; }

        // Lay nguoi dung dang dang nhap (null neu chua dang nhap)
        public static UserSession FromSession(HttpSessionStateBase session)
        {
            return session == null ? null : session["User"] as UserSession;
        }

        // Trang mac dinh cua tung vai tro sau khi dang nhap / khi bi tu choi quyen
        public static object HomeRoute(string role)
        {
            switch (role)
            {
                case UserRoles.Student: return new { controller = "StudentPortal", action = "Index" };
                case UserRoles.Staff: return new { controller = "Incidents", action = "Index" };
                default: return new { controller = "Home", action = "Index" };
            }
        }
    }
}
