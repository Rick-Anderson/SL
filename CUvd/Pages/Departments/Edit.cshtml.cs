using ContosoUniversity.Data;
using ContosoUniversity.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace ContosoUniversity.Pages.Departments
{
    public class EditModel : PageModel
    {
        private readonly ContosoUniversity.Data.SchoolContext _context;

        public EditModel(ContosoUniversity.Data.SchoolContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Department Department { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Department = await _context.Departments
                .Include(d => d.Administrator)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.DepartmentID == id);

            if (Department == null)
            {
                return NotFound();
            }

            // Scaffolder added ViewData, I'll remove it once code is working.
            ViewData["InstructorID"] = new SelectList(_context.Instructors, "ID", "FirstMidName");
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? id,
            [ModelBinder(Name = "Department.RowVersion")] byte[] rowVersion)
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var departmentToUpdate = await _context.Departments
                .Include(i => i.Administrator)
                .FirstOrDefaultAsync(m => m.DepartmentID == id);

            // null means Department was deleted by another user.
            if (departmentToUpdate == null)
            {
                Department deletedDepartment = new Department();
                // Fetch the posted data so we can display it with the error message.
                await TryUpdateModelAsync(deletedDepartment);
                CopyDepartment(deletedDepartment);
                ModelState.AddModelError(string.Empty,
                    "Unable to save. The department was deleted by another user.");
                ViewData["InstructorID"] = new SelectList(_context.Instructors, "ID", "FullName",
                    Department.InstructorID);
                return Page();
            }

            var rowVersionParmStr = rowVersion[7];
            var rowVersionOrgStr = ((byte[])(_context.Entry(departmentToUpdate)
                           .Property("RowVersion")).OriginalValue)[7];

            // OriginalValue is the current value in the DB when this entity
            // was fetched. OriginalValue == rowVersion unless there is a 
            // concurrency difference. rowVersion is the value when this record
            // was fetched by OnGetAsync. Set .OriginalValue = rowVersion to detect a 
            // concurrency problem. A second postback will make them match.
            _context.Entry(departmentToUpdate)
                .Property("RowVersion").OriginalValue = rowVersion;

            if (await TryUpdateModelAsync<Department>(
                departmentToUpdate,
                "Department",
                s => s.Name, s => s.StartDate, s => s.Budget, s => s.InstructorID))
            {
                try
                {
                    await _context.SaveChangesAsync();
                    return RedirectToPage("./Index");
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    var exceptionEntry = ex.Entries.Single();
                    var clientValues = (Department)exceptionEntry.Entity;
                    var databaseEntry = exceptionEntry.GetDatabaseValues();
                    if (databaseEntry == null)
                    {
                        ModelState.AddModelError(string.Empty,
                            "Unable to save changes. The department was deleted " +
                            "by another user.");
                        return Page();
                    }

                    var dbValues = (Department)databaseEntry.ToObject();
                    await setDbErrorMessage(dbValues, clientValues, _context);

                    var dbRowVers = ((byte[])dbValues.RowVersion)[7];

                    // Save the current RowVersion  so
                    // it can be posted back to this method in a hidden field.
                    Department.RowVersion = (byte[])dbValues.RowVersion;
                    ModelState.Remove("RowVersion");
                }
            }

            ViewData["InstructorID"] = new SelectList(_context.Instructors, "ID", "FullName", departmentToUpdate.InstructorID);
            return Page();
        }

        private void CopyDepartment(Department deletedDepartment)
        {
            Department.Administrator = deletedDepartment.Administrator;
            Department.Budget = deletedDepartment.Budget;
            Department.StartDate = deletedDepartment.StartDate;
            Department.InstructorID = deletedDepartment.InstructorID;
        }

        private async Task setDbErrorMessage(Department dbValues,
                Department clientValues, SchoolContext context)
        {

            if (dbValues.Name != clientValues.Name)
            {
                ModelState.AddModelError("Department.Name",
                    $"Current value: {dbValues.Name}");
            }
            if (dbValues.Budget != clientValues.Budget)
            {
                ModelState.AddModelError("Department.Budget",
                    $"Current value: {dbValues.Budget:c}");
            }
            if (dbValues.StartDate != clientValues.StartDate)
            {
                ModelState.AddModelError("Department.StartDate",
                    $"Current value: {dbValues.StartDate:d}");
            }
            if (dbValues.InstructorID != clientValues.InstructorID)
            {
                Instructor dbInstructor = await _context.Instructors
                   .FirstOrDefaultAsync(i => i.ID == dbValues.InstructorID);
                ModelState.AddModelError("Department.InstructorID",
                    $"Current value: {dbInstructor?.FullName}");
            }

            ModelState.AddModelError(string.Empty,
                   "The record you attempted to edit "
                 + "was modified by another user after you got the original value. The "
                 + "edit operation was canceled and the current values in the database "
                 + "have been displayed. If you still want to edit this record, click "
                 + "the Save button again. Otherwise click the Back to List hyperlink.");
        }

        public string RowVersionString(byte[] rowVersion)
        {
            string str = null;

            for (int i = 0; i < 8; i++)
            {
                if (rowVersion[i] != 0)
                    str += rowVersion[i].ToString();
            }

            return str;
        }
    }
}
