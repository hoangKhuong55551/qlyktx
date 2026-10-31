using System.Web;
using System.Web.Mvc;
using Qlykytucxa.Filters;

namespace Qlykytucxa
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            // Mac dinh moi trang deu phai dang nhap (tru action co [AllowAnonymous])
            filters.Add(new RoleAuthorizeAttribute());
        }
    }
}
