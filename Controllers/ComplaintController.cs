using CMS.Models;
using CMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace CMS.Controllers
{
    public class ComplaintController : Controller
    {
        private readonly MongoDbContext _context;
        private readonly ComplaintService _complaintService;
        private readonly IWebHostEnvironment _environment;

        public ComplaintController(MongoDbContext context, ComplaintService complaintService, IWebHostEnvironment environment)
        {
            _context = context;
            _complaintService = complaintService;
            _environment = environment;
        }

        [Authorize(Roles = "Helpdesk")]
        [HttpGet]
        public IActionResult DirectSubmit()
        {
            return View();
        }

        [Authorize(Roles = "Helpdesk")]
        [HttpPost]
        public async Task<IActionResult> DirectSubmit([FromForm] DirectComplaintViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Chandigarh-specific server-side validation
                if (model.State != "Chandigarh" || model.City != "Chandigarh")
                    return BadRequest(new { success = false, message = "Only Chandigarh is supported for registration." });
                
                if (!string.IsNullOrEmpty(model.PinCode) && !model.PinCode.StartsWith("160"))
                    return BadRequest(new { success = false, message = "Invalid Chandigarh Pin Code. Area must be within 160xxx series." });

                var allowedSources = new[] { "Mobile", "WhatsApp" };
                if (string.IsNullOrEmpty(model.Source) || !allowedSources.Contains(model.Source))
                    return BadRequest(new { success = false, message = "Invalid registration source." });

                // Note: Stop Automatic User Creation as requested.
                // We just use the provided contact info for the complaint.

                // 2. Handle File Attachment
                string? attachmentPath = null;
                if (model.Attachment != null && model.Attachment.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + model.Attachment.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.Attachment.CopyToAsync(fileStream);
                    }
                    attachmentPath = "/uploads/" + uniqueFileName;
                }

                var staffMobile = User.FindFirstValue(ClaimTypes.NameIdentifier);
                // 3. Create Complaint
                var complaint = new Complaint
                {
                    ComplaintNo = model.ComplaintNo ?? $"CHD/{DateTime.Now.Year}/{Guid.NewGuid().ToString().Substring(0, 5).ToUpper()}",
                    UserId = model.MobileNo,
                    RegisteredById = staffMobile,
                    FullName = model.FullName,
                    Email = model.Email,
                    ComplaintTitle = model.ComplaintTitle,
                    Description = model.Description,
                    State = model.State,
                    City = model.City,
                    Area = model.Area ?? model.AreaOfJurisdiction,
                    AreaOfJurisdiction = model.AreaOfJurisdiction ?? model.Area,
                    Department = model.Department,
                    Street = model.Street,
                    Locality = model.Locality,
                    PinCode = model.PinCode,
                    Site = model.Site,
                    Source = model.Source,
                    IncidentDate = model.IncidentDate,
                    AssignedToId = model.AssignedToId,
                    AssignedToName = model.AssignedToName,
                    AttachmentPath = attachmentPath,
                    CreatedDate = DateTime.UtcNow,
                    Status = string.IsNullOrEmpty(model.AssignedToId) ? "Pending" : "Assigned"
                };

                await _complaintService.CreateComplaintAsync(complaint);
                
                return Ok(new { success = true, complaintNo = complaint.ComplaintNo, message = "Successfully submitted with attachment! You can track this by logging in." });
            }
            return BadRequest(new { success = false, message = "Invalid data provided." });
        }

        [Authorize]
        [HttpGet]
        public IActionResult Submit()
        {
            return RedirectToAction("DirectSubmit");
        }

        [HttpPost]
        public async Task<IActionResult> Submit([FromBody] Complaint model)
        {
            if (ModelState.IsValid)
            {
                // Chandigarh-specific server-side validation
                if (model.State != "Chandigarh" || model.City != "Chandigarh")
                    return BadRequest(new { success = false, message = "Only Chandigarh is supported for registration." });
                
                if (!string.IsNullOrEmpty(model.PinCode) && !model.PinCode.StartsWith("160"))
                    return BadRequest(new { success = false, message = "Invalid Chandigarh Pin Code. Area must be within 160xxx series." });

                var allowedSources = new[] { "Mobile", "WhatsApp" };
                if (string.IsNullOrEmpty(model.Source) || !allowedSources.Contains(model.Source))
                    return BadRequest(new { success = false, message = "Invalid registration source." });

                var userMobile = User.FindFirstValue(ClaimTypes.NameIdentifier);
                model.UserId = userMobile ?? "";

                await _complaintService.CreateComplaintAsync(model);
                
                return Ok(new { success = true, complaintNo = model.ComplaintNo });
            }
            return BadRequest(new { success = false, message = "Invalid data" });
        }

        [HttpGet]
        public async Task<IActionResult> GetNextId()
        {
            var nextId = await _complaintService.GetNextComplaintIdAsync();
            return Ok(new { nextId });
        }

        [Authorize(Roles = "Helpdesk,Citizen")]
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var userMobile = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRole = User.FindFirstValue(ClaimTypes.Role);

            var complaints = await _context.Complaints
                .Find(c => userRole == "Helpdesk" ? true : c.UserId == userMobile) // Helpdesk sees all, Citizen sees self
                .SortByDescending(c => c.CreatedDate)
                .ToListAsync();

            if (userRole == "Helpdesk")
            {
                var allPending = await _context.Complaints
                    .Find(c => string.IsNullOrEmpty(c.AssignedToId))
                    .SortByDescending(c => c.CreatedDate)
                    .ToListAsync();
                ViewBag.AllPending = allPending;
                ViewBag.UnassignedCount = allPending.Count;
            }

            ViewBag.Total = complaints.Count;
            ViewBag.Pending = complaints.Count(c => c.Status == "Pending");
            ViewBag.Assigned = complaints.Count(c => c.Status == "Assigned");
            ViewBag.Resolved = complaints.Count(c => c.Status == "Resolved");

            if (userRole == "Helpdesk")
            {
                ViewBag.Users = await _context.Users.Find(_ => true).ToListAsync();
                ViewBag.Heads = await _context.Users.Find(u => u.Role == "DeptHead").ToListAsync();
                var masters = await _context.Masters.Find(_ => true).FirstOrDefaultAsync() ?? new Master();
                ViewBag.Masters = masters;
                ViewBag.AllDepartments = masters.Departments.Select(d => new CMS.Models.DepartmentBrief { Name = d.Name, Code = d.Code }).ToList();
                return View("HelpdeskDashboard", complaints);
            }


            return View(complaints);
        }

        [Authorize(Roles = "Helpdesk,Admin")]
        [HttpPost]
        public async Task<IActionResult> Reassign(string complaintId, string headId, string headName)
        {
            var filter = Builders<Complaint>.Filter.Eq(c => c.Id, complaintId);
            var officer = await _context.Users.Find(u => u.Id == headId).FirstOrDefaultAsync();
            var update = Builders<Complaint>.Update
                .Set(c => c.AssignedToId, headId)
                .Set(c => c.AssignedToName, headName)
                .Set(c => c.AssignedToMobile, officer?.MobileNo)
                .Set(c => c.Status, "Assigned");

            await _context.Complaints.UpdateOneAsync(filter, update);
            return Json(new { success = true });
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Track(string id)
        {
            if (string.IsNullOrEmpty(id)) return BadRequest(new { success = false, message = "Complaint number is required." });

            var complaint = await _context.Complaints
                .Find(c => c.ComplaintNo == id)
                .FirstOrDefaultAsync();

            if (complaint == null) return NotFound(new { success = false, message = "No complaint found with this number." });

            string timeTaken = "—";
            if (complaint.ResolutionDate.HasValue)
            {
                var diff = complaint.ResolutionDate.Value - complaint.CreatedDate;
                timeTaken = $"{(int)diff.TotalDays}d {diff.Hours}h {diff.Minutes}m";
            }

            return Ok(new {
                success = true,
                complaintNo = complaint.ComplaintNo,
                status = complaint.Status,
                department = complaint.Department,
                createdDate = complaint.CreatedDate.ToString("dd/MM/yyyy HH:mm:ss"),
                fullName = complaint.FullName ?? "—",
                address = $"{complaint.Locality}, {complaint.PinCode}",
                title = complaint.ComplaintTitle,
                assignedTo = complaint.AssignedToName ?? "Pending",
                assignedToMobile = complaint.AssignedToMobile ?? "—",
                resolutionDate = complaint.ResolutionDate?.ToString("dd/MM/yyyy HH:mm:ss") ?? "—",
                resolutionRemark = complaint.ResolutionRemark ?? "—",
                timeTaken = timeTaken
            });
        }

        [Authorize(Roles = "Helpdesk,Admin")]
        [HttpGet]
        public async Task<IActionResult> GetComplaintByNo(string complaintNo)
        {
            var complaint = await _context.Complaints.Find(c => c.ComplaintNo == complaintNo).FirstOrDefaultAsync();
            if (complaint == null) return NotFound(new { success = false, message = "Complaint not found." });
            return Ok(new { success = true, complaint });
        }

        [Authorize(Roles = "Helpdesk,Admin")]
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(string complaintId, string status, string? remark)
        {
            var filter = Builders<Complaint>.Filter.Eq(c => c.Id, complaintId);
            var update = Builders<Complaint>.Update.Set(c => c.Status, status);
            
            if (status == "Resolved") {
                update = update.Set(c => c.ResolutionDate, System.DateTime.UtcNow)
                               .Set(c => c.ResolutionRemark, remark);
            }

            await _context.Complaints.UpdateOneAsync(filter, update);
            return Json(new { success = true });
        }

        [Authorize(Roles = "Helpdesk,Admin")]
        [HttpPost]
        public async Task<IActionResult> UpdatePriority(string complaintId, string priority)
        {
            var filter = Builders<Complaint>.Filter.Eq(c => c.Id, complaintId);
            var update = Builders<Complaint>.Update.Set(c => c.Priority, priority);

            await _context.Complaints.UpdateOneAsync(filter, update);
            return Json(new { success = true });
        }
    }
}
