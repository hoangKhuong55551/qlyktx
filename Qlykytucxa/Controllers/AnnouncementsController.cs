using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using Newtonsoft.Json;
using Qlykytucxa.Filters;
using Qlykytucxa.Models;

namespace Qlykytucxa.Controllers
{
    [RoleAuthorize(UserRoles.Admin)]
    public class AnnouncementsController : Controller
    {
        private static readonly object _fileLock = new object();

        private string GetFilePath()
        {
            string appData = Server.MapPath("~/App_Data");
            if (!Directory.Exists(appData))
            {
                Directory.CreateDirectory(appData);
            }
            return Path.Combine(appData, "announcements.json");
        }

        public List<Announcement> LoadAnnouncements()
        {
            lock (_fileLock)
            {
                string path = GetFilePath();
                if (!System.IO.File.Exists(path))
                {
                    // Dữ liệu mẫu ban đầu
                    var defaults = new List<Announcement>
                    {
                        new Announcement
                        {
                            Id = 1,
                            Title = "Thông báo: Tạm ngừng cấp nước phục vụ súc rửa bể chứa tòa A",
                            Category = "Bảo trì",
                            Content = "Ban Quản lý Ký túc xá thông báo lịch súc rửa, khử khuẩn định kỳ bể nước sinh hoạt tòa A. Nước sinh hoạt sẽ tạm ngưng từ 08h00 đến 11h30 ngày 30/10. Đề nghị các phòng chủ động tích trữ nước dùng.",
                            Priority = "Khẩn cấp",
                            CreatedAt = DateTime.Now.AddDays(-1),
                            AuthorName = "Ban Quản lý KTX",
                            IsActive = true
                        },
                        new Announcement
                        {
                            Id = 2,
                            Title = "Nhắc nhở: Hạn chót thanh toán hóa đơn tiền điện nước tháng này",
                            Category = "Điện nước",
                            Content = "Hạn chót thanh toán tiền điện nước tháng này là ngày 15 hàng tháng. Đề nghị đại diện các phòng kiểm tra hóa đơn và quét mã VietQR tự động để thanh toán đúng hạn tránh phát sinh tiền phạt chậm nộp.",
                            Priority = "Quan trọng",
                            CreatedAt = DateTime.Now.AddDays(-3),
                            AuthorName = "Bộ phận Tài chính KTX",
                            IsActive = true
                        },
                        new Announcement
                        {
                            Id = 3,
                            Title = "Kế hoạch tổng vệ sinh và chấm điểm phòng ở sạch đẹp tháng 10",
                            Category = "Vệ sinh",
                            Content = "Vào sáng thứ Bảy tuần này, Ban Quản lý sẽ tiến hành kiểm tra và chấm điểm vệ sinh các phòng ở toàn bộ các tòa nhà KTX. Các phòng đạt điểm xuất sắc sẽ được biểu dương và cộng điểm rèn luyện.",
                            Priority = "Thông thường",
                            CreatedAt = DateTime.Now.AddDays(-5),
                            AuthorName = "Đoàn Thanh niên KTX",
                            IsActive = true
                        }
                    };
                    SaveAnnouncements(defaults);
                    return defaults;
                }

                string json = System.IO.File.ReadAllText(path);
                return JsonConvert.DeserializeObject<List<Announcement>>(json) ?? new List<Announcement>();
            }
        }

        private void SaveAnnouncements(List<Announcement> list)
        {
            lock (_fileLock)
            {
                string path = GetFilePath();
                string json = JsonConvert.SerializeObject(list, Formatting.Indented);
                System.IO.File.WriteAllText(path, json);
            }
        }

        // GET: Announcements (Ky thuat vien duoc xem, chi Admin duoc dang/sua/xoa)
        [RoleAuthorize(UserRoles.Admin, UserRoles.Staff)]
        public ActionResult Index(string categoryFilter)
        {
            var list = LoadAnnouncements();
            if (!string.IsNullOrEmpty(categoryFilter))
            {
                list = list.Where(a => a.Category == categoryFilter).ToList();
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
                var list = LoadAnnouncements();
                int nextId = list.Any() ? list.Max(x => x.Id) + 1 : 1;
                announcement.Id = nextId;
                announcement.CreatedAt = DateTime.Now;

                list.Insert(0, announcement);
                SaveAnnouncements(list);

                TempData["Success"] = "Đăng thông báo mới lên bảng tin KTX thành công!";
                return RedirectToAction("Index");
            }
            return View(announcement);
        }

        // GET: Announcements/Edit/5
        public ActionResult Edit(int id)
        {
            var item = LoadAnnouncements().FirstOrDefault(a => a.Id == id);
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
                var list = LoadAnnouncements();
                var existing = list.FirstOrDefault(a => a.Id == announcement.Id);
                if (existing != null)
                {
                    existing.Title = announcement.Title;
                    existing.Category = announcement.Category;
                    existing.Content = announcement.Content;
                    existing.Priority = announcement.Priority;
                    existing.IsActive = announcement.IsActive;
                    existing.AuthorName = announcement.AuthorName;
                    SaveAnnouncements(list);

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
            var list = LoadAnnouncements();
            var item = list.FirstOrDefault(a => a.Id == id);
            if (item != null)
            {
                list.Remove(item);
                SaveAnnouncements(list);
                TempData["Success"] = "Đã gỡ bỏ thông báo khỏi bảng tin!";
            }
            return RedirectToAction("Index");
        }
    }
}
