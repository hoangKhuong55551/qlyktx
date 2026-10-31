using System;
using System.Linq;
using System.Web.Mvc;
using Qlykytucxa.Filters;
using Qlykytucxa.Models;

namespace Qlykytucxa.Controllers
{
    [RoleAuthorize(UserRoles.Admin)]
    public class AnnouncementsController : Controller
    {
        private DormExtContext ext = new DormExtContext();

        // GET: Announcements (Ky thuat vien duoc xem, chi Admin duoc dang/sua/xoa)
        [RoleAuthorize(UserRoles.Admin, UserRoles.Staff)]
        public ActionResult Index(string categoryFilter)
        {
            var list = ext.Announcements.AsQueryable();
            if (!string.IsNullOrEmpty(categoryFilter))
            {
                list = list.Where(a => a.Category == categoryFilter);
            }
            ViewBag.CategoryFilter = categoryFilter;
            return View(list.OrderByDescending(a => a.CreatedAt).ToList());
        }

        // GET: Announcements/Create
        public ActionResult Create()
        {
            return View(new Announcement
            {
                CreatedAt = DateTime.Now,
                Priority = "Thông thường",
                IsActive = true,
                AuthorName = "Ban Quản lý KTX"
            });
        }

        // POST: Announcements/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Announcement announcement)
        {
            if (ModelState.IsValid)
            {
                announcement.CreatedAt = DateTime.Now;
                ext.Announcements.Add(announcement);
                ext.SaveChanges();

                TempData["Success"] = "Đăng thông báo mới lên bảng tin KTX thành công!";
                return RedirectToAction("Index");
            }
            return View(announcement);
        }

        // GET: Announcements/Edit/5
        public ActionResult Edit(int id)
        {
            var item = ext.Announcements.Find(id);
            if (item == null) return HttpNotFound();
            return View(item);
        }

        // POST: Announcements/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Announcement announcement)
        {
            if (ModelState.IsValid)
            {
                var existing = ext.Announcements.Find(announcement.Id);
                if (existing != null)
                {
                    existing.Title = announcement.Title;
                    existing.Category = announcement.Category;
                    existing.Content = announcement.Content;
                    existing.Priority = announcement.Priority;
                    existing.IsActive = announcement.IsActive;
                    existing.AuthorName = announcement.AuthorName;
                    ext.SaveChanges();

                    TempData["Success"] = "Cập nhật thông báo thành công!";
                    return RedirectToAction("Index");
                }
                return HttpNotFound();
            }
            return View(announcement);
        }

        // POST: Announcements/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var item = ext.Announcements.Find(id);
            if (item != null)
            {
                ext.Announcements.Remove(item);
                ext.SaveChanges();
                TempData["Success"] = "Đã gỡ bỏ thông báo khỏi bảng tin!";
            }
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) ext.Dispose();
            base.Dispose(disposing);
        }
    }
}
