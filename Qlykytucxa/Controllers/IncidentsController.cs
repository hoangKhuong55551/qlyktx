using System;
using System.Linq;
using System.Web.Mvc;
using Qlykytucxa.Filters;
using Qlykytucxa.Models;

namespace Qlykytucxa.Controllers
{
    [RoleAuthorize(UserRoles.Admin, UserRoles.Staff)]
    public class IncidentsController : Controller
    {
        private MiniDormDBEntities db = new MiniDormDBEntities();
        private DormExtContext ext = new DormExtContext();

        // GET: Incidents
        public ActionResult Index(string searchString, string statusFilter, string categoryFilter)
        {
            var list = ext.IncidentRequests.AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                list = list.Where(i => i.RoomNumber.Contains(searchString)
                                    || i.Title.Contains(searchString)
                                    || i.ReporterName.Contains(searchString));
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
            var room = db.Rooms.Find(incident.RoomId);
            if (room == null)
            {
                ModelState.AddModelError("RoomId", "Phòng không tồn tại!");
            }

            if (ModelState.IsValid)
            {
                incident.RoomNumber = room.RoomNumber;
                incident.CreatedAt = DateTime.Now;
                incident.Status = DormConstants.IncidentMoiTiepNhan;
                incident.ResolvedAt = null;
                if (string.IsNullOrEmpty(incident.AssignedTo))
                {
                    incident.AssignedTo = DormConstants.ChuaPhanCong;
                }

                ext.IncidentRequests.Add(incident);
                ext.SaveChanges();

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
            var incident = ext.IncidentRequests.Find(id);
            if (incident == null) return HttpNotFound();

            ViewBag.RoomId = new SelectList(db.Rooms.OrderBy(r => r.RoomNumber), "RoomId", "RoomNumber", incident.RoomId);
            return View(incident);
        }

        // POST: Incidents/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(IncidentRequest incident)
        {
            var room = db.Rooms.Find(incident.RoomId);
            if (room == null)
            {
                ModelState.AddModelError("RoomId", "Phòng không tồn tại!");
            }
            if (incident.Cost < 0)
            {
                ModelState.AddModelError("Cost", "Chi phí sửa chữa không được âm!");
            }

            if (ModelState.IsValid)
            {
                var item = ext.IncidentRequests.Find(incident.Id);
                if (item != null)
                {
                    item.RoomId = incident.RoomId;
                    item.RoomNumber = room.RoomNumber;
                    item.Title = incident.Title;
                    item.Category = incident.Category;
                    item.Description = incident.Description;
                    item.ReporterName = incident.ReporterName;
                    item.AssignedTo = incident.AssignedTo;
                    item.Cost = incident.Cost;

                    // Nếu đổi trạng thái sang "Đã hoàn thành" mà chưa có ngày hoàn thành -> lấy giờ hiện tại
                    if (incident.Status == DormConstants.IncidentHoanThanh && item.Status != DormConstants.IncidentHoanThanh)
                    {
                        item.ResolvedAt = DateTime.Now;
                    }
                    else if (incident.Status != DormConstants.IncidentHoanThanh)
                    {
                        item.ResolvedAt = null;
                    }

                    item.Status = incident.Status;

                    ext.SaveChanges();
                    TempData["Success"] = "Cập nhật trạng thái sự cố thành công!";
                    return RedirectToAction("Index");
                }
                return HttpNotFound();
            }

            ViewBag.RoomId = new SelectList(db.Rooms.OrderBy(r => r.RoomNumber), "RoomId", "RoomNumber", incident.RoomId);
            return View(incident);
        }

        // POST: Incidents/Delete/5
        [HttpPost]
        [RoleAuthorize(UserRoles.Admin)]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var item = ext.IncidentRequests.Find(id);
            if (item != null)
            {
                ext.IncidentRequests.Remove(item);
                ext.SaveChanges();
                TempData["Success"] = "Đã xóa yêu cầu sự cố!";
            }
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
                ext.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
