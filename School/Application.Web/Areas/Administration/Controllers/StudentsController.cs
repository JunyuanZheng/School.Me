﻿using Application.Common;
using Application.Models;
using Application.Services.Interfaces;
using Application.Web.Areas.Administration.Models;
using Application.Web.Areas.Students.Controllers;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNet.Identity;
using PagedList;
using System;
using System.Linq;
using System.Net;
using System.Web.Mvc;

namespace Application.Web.Areas.Administration.Controllers
{
    [Authorize(Roles = GlobalConstants.AdministratorRoleName)]
    public class StudentsController : Controller
    {
        private readonly IStudentService studentService;

        public StudentsController(IStudentService studentService)
        {
            this.studentService = studentService;
        }

        // GET: Administration/Students
        public ActionResult Index(string sortOrder, string currentFilter, string searchString, int? page)
        {
            ViewBag.CurrentSort = sortOrder;
            ViewBag.UserNameSortParam = String.IsNullOrEmpty(sortOrder) ? "username_desc" : "";
            ViewBag.NameSortParam = sortOrder == "name" ? "name_desc" : "name";

            if (searchString != null)
            {
                page = 1;
            }
            else
            {
                searchString = currentFilter;
            }

            ViewBag.CurrentFilter = searchString;

            IQueryable<Student> students = this.studentService.All();

            if (!String.IsNullOrEmpty(searchString))
            {
                students = students
                    .Where(s => s.ApplicationUser.UserName.Contains(searchString) || s.Name.Contains(searchString));
            }

            switch (sortOrder)
            {
                case "username_desc":
                    students = students.OrderByDescending(s => s.ApplicationUser.UserName);
                    break;
                case "name":
                    students = students.OrderBy(s => s.Name);
                    break;
                case "name_desc":
                    students = students.OrderByDescending(s => s.Name);
                    break;
                default:
                    students = students.OrderBy(s => s.ApplicationUser.UserName);
                    break;
            }

            IQueryable<StudentListViewModel> sortedStudents = students.Project().To<StudentListViewModel>();

            int pageSize = 10;
            int pageIndex = (page ?? 1);

            return View(sortedStudents.ToPagedList(pageIndex, pageSize));
        }

        public ActionResult Details(string username)
        {
            if (username == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            Student student = this.studentService.GetByUserName(username);

            if (student == null)
            {
                return HttpNotFound();
            }

            StudentDetailsEditModel model = Mapper.Map<Student, StudentDetailsEditModel>(student);
            model.AccountDetailsEditModel = Mapper.Map<ApplicationUser, AccountDetailsEditModel>(student.ApplicationUser);

            return View(model);
        }

        public ActionResult Edit(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                ModelState.AddModelError("", "No user has been selected");
                return View();
            }

            Student student = this.studentService.GetByUserName(username);

            if (student == null)
            {
                ModelState.AddModelError("", "Such user does not exist");
                return View();
            }

            StudentDetailsEditModel studentModel = Mapper.Map<Student, StudentDetailsEditModel>(student);
            studentModel.AccountDetailsEditModel = Mapper.Map<ApplicationUser, AccountDetailsEditModel>(student.ApplicationUser);

            return View(studentModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(StudentDetailsEditModel studentModel)
        {
            if (!ModelState.IsValid)
            {
                ModelState.AddModelError("", "User data has not been filled correctly. Please, re-enter");
                return View(studentModel);
            }

            Student student = this.studentService.GetById(studentModel.Id);

            if (student == null)
            {
                ModelState.AddModelError("", "No such user exists");
                return View();
            }

            bool isUserNameUnique = this.studentService.IsUserNameUniqueOnEdit(
                student, 
                studentModel.AccountDetailsEditModel.UserName);

            if (!isUserNameUnique)
            {
                this.ModelState.AddModelError("AccountDetailsEditModel.UserName", "Duplicate usernames are not allowed.");
                return View();
            }

            Mapper.Map<StudentDetailsEditModel, Student>(studentModel, student);
            Mapper.Map<AccountDetailsEditModel, ApplicationUser>(studentModel.AccountDetailsEditModel, student.ApplicationUser);
            this.studentService.Update(student);

            return RedirectToAction("Index", "Students");
        }

        public ActionResult Delete(Guid id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            Student student = this.studentService.GetById(id);

            if (student == null)
            {
                return HttpNotFound();
            }
            return View(student);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(Guid id)
        {
            Student student = this.studentService.GetById(id);

            if (student.ApplicationUserId == User.Identity.GetUserId())
            {
                student.DeletedBy = User.Identity.GetUserId();
                this.studentService.Delete(student);

                var accountController = new AccountController(this.studentService);
                accountController.ControllerContext = this.ControllerContext;
                accountController.LogOff();

                return RedirectToAction("Index", "Home");
            }

            student.DeletedBy = User.Identity.GetUserId();
            this.studentService.Delete(student);
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            this.studentService.Dispose();
            base.Dispose(disposing);
        }
    }
}