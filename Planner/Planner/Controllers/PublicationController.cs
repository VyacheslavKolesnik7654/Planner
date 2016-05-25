﻿using Domain.Helpers;
using Domain.Models;
using Domain.Models.Enums;
using Domain.Reports;
using Planner.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Calculation;
using System.IO;

namespace Planner.Controllers
{
	public class PublicationController : Controller
	{
		private ApplicationUser user;
		protected override void Initialize(RequestContext requestContext)
		{
			base.Initialize(requestContext);
			using (ApplicationDbContext db = new ApplicationDbContext())
			{
				user = db.Users.FirstOrDefault(x => x.UserName == requestContext.HttpContext.User.Identity.Name);
			}

		}
		// GET: Publication
		public ActionResult Index()
		{
			return View(PublicationReportBuilder.CreateForm11(user));
		}

		public ActionResult Create()
		{
			using (ApplicationDbContext db = new ApplicationDbContext())
			{
				var model = new CreatePublicationViewModel
				{
					ScientificBases = db.ScientificBases.ToList(),
				};

				return View(model);

			}

		}

		[HttpPost]
		public ActionResult Create(PublicationCreate model, HttpPostedFileBase file)
		{
			using (ApplicationDbContext db = new ApplicationDbContext())
			{
				var filepath = ConfigurationManager.AppSettings["PublicationFolder"] + new Random().Next() + file.FileName.Substring(file.FileName.LastIndexOf('.'));
				try
				{

					var a = Server.MapPath(filepath);
					file.SaveAs(Server.MapPath(filepath));
					Publication publication = new Publication()
					{
						Name = model.Name,
						FilePath = filepath,
						Pages = model.Pages,
						Output = model.Output,
						StoringType = new StoringType() { Value = model.StoringType },
						PublicationType = new PublicationType() { Value = model.PublicationType },
						CreatedAt = DateTime.Now.ToUniversalTime(),
						PublishedAt = DateTime.Now.ToUniversalTime(),
						IsPublished = true,
						PublicationScientificBases = new List<PublicationScientificBase>()
						{
							new PublicationScientificBase()
							{
								ScientificBaseId = model.ScientificBaseId
							}
						},
						PublicationUsers = new List<PublicationUser>
						{
							new PublicationUser()
							{
								UserId = user.Id
							}
						}
					};
					if (model.CollaboratorsIds != null)
					{
						foreach (var collab in model.CollaboratorsIds.Where(x => x != String.Empty && x.Substring(0, 2) == "c_"))
						{
							publication.PublicationUsers.Add(new PublicationUser()
							{
								CollaboratorId = collab.Substring(2),
								Publication = publication
							});
						}
						foreach (var collab in model.CollaboratorsIds.Where(x => x != String.Empty && x.Substring(0, 2) == "u_"))
						{
							publication.PublicationUsers.Add(new PublicationUser()
							{
								UserId = collab.Substring(2),
								Publication = publication
							});
						}
					}
					if (model.NewCollaboratorsNames != null)
					{
						foreach (var collab in model.NewCollaboratorsNames.Where(x => x != String.Empty))
						{
							var newcollab = new ExternalCollaborator() { Name = collab };
							db.ExternalCollaborators.Add(newcollab);
							publication.PublicationUsers.Add(new PublicationUser()
							{
								Collaborator = newcollab,
								Publication = publication
							});
						}
					}


					db.Publications.Add(publication);
					db.SaveChanges();
				}
				catch (Exception ex)
				{
					System.IO.File.Delete(filepath);
				}

				return RedirectToAction("Index");
			}
		}


		//does not work yet
		private ActionResult Draft()
		{
			using (ApplicationDbContext db = new ApplicationDbContext())
			{
				var model = db.Publications
					.Include("StoringType")
					.Include("PublicationType")
					.Where(x => x.IsPublished == true)
					.AsEnumerable()
					.Select(x => new PublicationDraft()
					{
						Id = x.Id,
						FilePath = x.FilePath,
						Name = x.Name,
						Pages = x.Pages.HasValue ? x.Pages.Value : x.Pages.Value,
						Output = x.Output,
						StoringType = ((DisplayAttribute)typeof(StoringTypeEnum)
								.GetMember(x.StoringType.Value.ToString())[0]
								.GetCustomAttributes(typeof(DisplayAttribute), false)[0]).Name,
						PublicationType = ((DisplayAttribute)typeof(StoringTypeEnum)
								.GetMember(x.PublicationType.Value.ToString())[0]
								.GetCustomAttributes(typeof(DisplayAttribute), false)[0]).Name,
						CreatedAt = x.CreatedAt,
						Collaborators = db.PublicationUsers
							.Where(p => p.PublicationId == x.Id)
							.Join(db.Publications, pu => pu.PublicationId, p => p.Id, (pu, p) => new { pu, p })
							.Join(db.Users, pup => pup.pu.UserId, u => u.Id, (pup, u) => new { pup, u })
							.Join(db.ExternalCollaborators, pu => pu.pup.pu.CollaboratorId, c => c.Id, (pu, c) => new { pu, c })
							.Select(pu => new Author()
							{
								UserId = pu.pu.u.Id,
								CollaboratorId = pu.c.Id,
								Name = pu.pu.u.Id != null ? $"{pu.pu.u.LastName} {pu.pu.u.FirstName} {pu.pu.u.ThirdName}"
														: pu.c.Name
							})
							.ToList()
					})
					.ToList();

				return View();
			}
		}

		public FileResult PrintForm11()
		{
			var filestream = PublicationReportBuilder.ReportForm11(user);
			
			return File(filestream, "application/vnd.ms-excel", $"Публикации {user.LastName} {user.FirstName.Substring(0, 1)}. {user.ThirdName.Substring(0, 1)}. - {DateTime.Now.ToShortDateString()}.xls");
				
							
		}
	}
}