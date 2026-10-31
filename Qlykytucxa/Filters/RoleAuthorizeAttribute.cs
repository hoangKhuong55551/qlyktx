using System;
using System.Linq;
using System.Web.Mvc;
using System.Web.Routing;
using Qlykytucxa.Models;

namespace Qlykytucxa.Filters
{
    // Bat buoc dang nhap truoc khi vao action. Neu truyen danh sach vai tro thi chi cac vai tro do moi duoc vao.
    // Gan o cap action se thay the gan o cap controller, cap controller thay the cap global (AllowMultiple = false).
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class RoleAuthorizeAttribute : FilterAttribute, IAuthorizationFilter
    {
        private readonly string[] _roles;

        public RoleAuthorizeAttribute(params string[] roles)
        {
            _roles = roles ?? new string[0];
        }

        public void OnAuthorization(AuthorizationContext filterContext)
        {
            if (filterContext.ActionDescriptor.IsDefined(typeof(AllowAnonymousAttribute), true)
                || filterContext.ActionDescriptor.ControllerDescriptor.IsDefined(typeof(AllowAnonymousAttribute), true))
            {
                return;
            }

            var user = UserSession.FromSession(filterContext.HttpContext.Session);
            if (user == null)
            {
                var request = filterContext.HttpContext.Request;
                string returnUrl = request.HttpMethod == "GET" ? request.RawUrl : null;
                filterContext.Controller.TempData["Error"] = "Vui lòng đăng nhập để tiếp tục!";
                filterContext.Result = new RedirectToRouteResult(new RouteValueDictionary(new { controller = "Account", action = "Login", returnUrl }));
                return;
            }

            if (_roles.Length > 0 && !_roles.Contains(user.Role))
            {
                filterContext.Controller.TempData["Error"] = "Bạn không có quyền truy cập chức năng này!";
                filterContext.Result = new RedirectToRouteResult(new RouteValueDictionary(UserSession.HomeRoute(user.Role)));
            }
        }
    }
}
