using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using Newtonsoft.Json;
using Qlykytucxa.Models;

namespace Qlykytucxa.Controllers
{
    public class IncidentsController : Controller
    {
        private MiniDormDBEntities db = new MiniDormDBEntities();
        private static readonly object _fileLock = new object();

        private string GetFilePath()
        {
            string appData = Server.MapPath("~/App_Data");
            if (!Directory.Exists(appData))
            {
                Directory.CreateDirectory(appData);
            }
            return Path.Combine(appData, "incidents.json");
        }

        private List<IncidentRequest> LoadIncidents()
        {
            lock (_fileLock)
            {
                string path = GetFilePath();
                if (!System.IO.File.Exists(path))
                {
                    // Sinh dữ liệu mẫu thực tế ban đầu
                    var defaultList = new List<IncidentRequest>
                    {
                        new IncidentRequest
                        {
                            Id = 1,
                            RoomId = 1,
                            RoomNumber = "A101",
                            Title = "Hỏng vòi hoa sen nhà tắm",
                            Category = "Nước",
                            Description = "Vòi nước bị rò rỉ nước liên tục, cần thay van mới.",
                            ReporterName = "Nguyễn Văn An",
                            CreatedAt = DateTime.Now.AddDays(-2),
                            AssignedTo = "Chú Hùng (Thợ nước)",
                            Status = "Đang xử lý",
                            Cost = 150000,
                            ResolvedAt = null
                        },
                        new IncidentRequest
                        {
                            Id = 2,
                            RoomId = 2,
                            RoomNumber = "A102",
                            Title = "Điều hòa không mát, kêu to",
                            Category = "Điều hòa",
                            Description = "Bật 18 độ nhưng chỉ ra gió, cục nóng kêu rất to.",
                            ReporterName = "Trần Thị Bình",
                            CreatedAt = DateTime.Now.AddHours(-10),
                            AssignedTo = "Chưa phân công",
                            Status = "Mới tiếp nhận",
                            Cost = 0,
                            ResolvedAt = null
                        },
                        new IncidentRequest
                        {
                            Id = 3,
                            RoomId = 3,
                            RoomNumber = "B201",
                            Title = "Cháy bóng đèn tuýp phòng ngủ",
                            Category = "Điện",
                            Description = "Bóng đèn nhấp nháy rồi tắt hẳn sáng nay.",
                            ReporterName = "Lê Hoàng Long",
                            CreatedAt = DateTime.Now.AddDays(-4),
                            AssignedTo = "Chú Tuấn (Thợ điện)",
                            Status = "Đã hoàn thành",
                            Cost = 80000,
                            ResolvedAt = DateTime.Now.AddDays(-3)
                        }
                    };
                    System.IO.File.WriteAllText(path, JsonConvert.SerializeObject(defaultList, Formatting.Indented));
                    return defaultList;
                }

                string json = System.IO.File.ReadAllText(path);
                return JsonConvert.DeserializeObject<List<IncidentRequest>>(json) ?? new List<IncidentRequest>();
            }
        }

        private void SaveIncidents(List<IncidentRequest> list)
        {
            lock (_fileLock)
            {
                string path = GetFilePath();
                System.IO.File.WriteAllText(path, JsonConvert.SerializeObject(list, Formatting.Indented));
            }
        }

        // GET: Incidents
        public ActionResult Index(string searchString, string statusFilter, string categoryFilter)
        {
            var list = LoadIncidents().AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                list = list.Where(i => (i.RoomNumber != null && i.RoomNumber.Contains(searchString))
                                    || (i.Title != null && i.Title.Contains(searchString))
                                    || (i.ReporterName != null && i.ReporterName.Contains(searchString)));
            }

            if (!string.IsNullOrEmpty(statusFilter))
            {
                list = list.Where(i => i.Status == statusFilter);
            }

            if (!string.IsNullOrEmpty(categoryFilter))
            {
                list = list.Where(i => i.Category == categoryFilter);
            }

            ViewBag.SearchString = searchString;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.CategoryFilter = categoryFilter;

            return View(list.OrderByDescending(i => i.CreatedAt).ToList());
        }

        // GET: Incidents/Create
        public ActionResult Create()
        {
            ViewBag.RoomId = new SelectList(db.Rooms.OrderBy(r => r.RoomNumber), "RoomId", "RoomNumber");
            return View();
        }

        // POST: Incidents/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(IncidentRequest incident)
        {
            if (ModelState.IsValid)
            {
                var room = db.Rooms.Find(incident.RoomId);
                incident.RoomNumber = room != null ? room.RoomNumber : "N/A";
                incident.CreatedAt = DateTime.Now;
                incident.Status = "Mới tiếp nhận";
                if (string.IsNullOrEmpty(incident.AssignedTo))
                {
                    incident.AssignedTo = "Chưa phân công";
                }

                var list = LoadIncidents();
                incident.Id = list.Any() ? list.Max(i => i.Id) + 1 : 1;
                list.Add(incident);
                SaveIncidents(list);

                TempData["Success"] = $"Gửi yêu cầu báo hỏng cho phòng {incident.RoomNumber} thành công!";
                return RedirectToAction("Index");
            }

            ViewBag.RoomId = new SelectList(db.Rooms.OrderBy(r => r.RoomNumber), "RoomId", "RoomNumber", incident.RoomId);
            return View(incident);
        }

        // GET: Incidents/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null) return HttpNotFound();
            var incident = LoadIncidents().FirstOrDefault(i => i.Id == id);
            if (incident == null) return HttpNotFound();

            ViewBag.RoomId = new SelectList(db.Rooms.OrderBy(r => r.RoomNumber), "RoomId", "RoomNumber", incident.RoomId);
            return View(incident);
        }

        // POST: Incidents/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(IncidentRequest incident)
        {
            if (ModelState.IsValid)
            {
                var list = LoadIncidents();
                var item = list.FirstOrDefault(i => i.Id == incident.Id);
                if (item != null)
                {
                    var room = db.Rooms.Find(incident.RoomId);
                    item.RoomId = incident.RoomId;
                    item.RoomNumber = room != null ? room.RoomNumber : item.RoomNumber;
                    item.Title = incident.Title;
                    item.Category = incident.Category;
                    item.Description = incident.Description;
                    item.ReporterName = incident.ReporterName;
                    item.AssignedTo = incident.AssignedTo;
                    item.Cost = incident.Cost;

                    // Nếu đổi trạng thái sang "Đã hoàn thành" mà chưa có ngày hoàn thành -> lấy giờ hiện tại
                    if (incident.Status == "Đã hoàn thành" && item.Status != "Đã hoàn thành")
                    {
                        item.ResolvedAt = DateTime.Now;
                    }
                    else if (incident.Status != "Đã hoàn thành")
                    {
                        item.ResolvedAt = null;
                    }

                    item.Status = incident.Status;

                    SaveIncidents(list);
                    TempData["Success"] = "Cập nhật trạng thái sự cố thành công!";
                    return RedirectToAction("Index");
                }
            }

            ViewBag.RoomId = new SelectList(db.Rooms.OrderBy(r => r.RoomNumber), "RoomId", "RoomNumber", incident.RoomId);
            return View(incident);
        }

        // POST: Incidents/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var list = LoadIncidents();
            var item = list.FirstOrDefault(i => i.Id == id);
            if (item != null)
            {
                list.Remove(item);
                SaveIncidents(list);
                TempData["Success"] = "Đã xóa yêu cầu sự cố!";
            }
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
