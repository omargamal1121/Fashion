using E_Commerce.Controllers;
using E_Commerce.DtoModels.Shared;
using E_Commerce.Interfaces;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using E_Commerce.Services.Category;

namespace E_Commerce.Services.SubCategoryServices
{
    public class SubCategoryLinkBuilder : BaseLinkBuilder, ISubCategoryLinkBuilder
    {
        protected override string ControllerName => "SubCategory";

        public SubCategoryLinkBuilder(IHttpContextAccessor context, LinkGenerator generator)
            : base(context, generator) { }

        public override List<LinkDto> GenerateLinks(int? id = null)
        {
            if (_context.HttpContext == null)
                return new List<LinkDto>();

            var links = new List<LinkDto>
            {
                new LinkDto(
                    GetUriByAction(nameof(SubCategoryController.CreateAsync)) ?? "",
                    "create",
                    "POST"
                ),
                new LinkDto(
                    GetUriByAction(nameof(SubCategoryController.GetAllForUser)) ?? "",
                    "get-all",
                    "GET"
                ),
              
               
            };

            if (id != null)
            {
                links.Add(
                    new LinkDto(
                        GetUriByAction(nameof(SubCategoryController.GetByIdAsync), new { id }) ?? "",
                        "get-by-id",
                        "GET"
                    )
                );
                links.Add(
                    new LinkDto(
                        GetUriByAction(nameof(SubCategoryController.DeleteAsync), new { id }) ?? "",
                        "delete",
                        "DELETE"
                    )
                );
                links.Add(
                    new LinkDto(
                        GetUriByAction(nameof(SubCategoryController.UpdateAsync), new { id }) ?? "",
                        "update",
                        "PUT"
                    )
                );
                links.Add(
                    new LinkDto(
                        GetUriByAction(nameof(SubCategoryController.ReturnRemovedSubCategoryAsync), new { id }) ?? "",
                        "restore",
                        "PATCH"
                    )
                );
                links.Add(
                    new LinkDto(
                        GetUriByAction(nameof(SubCategoryController.AddMainImageAsync), new { id }) ?? "",
                        "add-main-image",
                        "POST"
                    )
                );
                links.Add(
                    new LinkDto(
                        GetUriByAction(nameof(SubCategoryController.AddExtraImagesAsync), new { id }) ?? "",
                        "add-extra-images",
                        "POST"
                    )
                );
                links.Add(
                    new LinkDto(
                        GetUriByAction(nameof(SubCategoryController.Activate), new { subCategoryId = id }) ?? "",
                        "change-active-status",
                        "PATCH"
                    )
                );
                links.Add(
                    new LinkDto(
                        GetUriByAction(nameof(SubCategoryController.Deactivate), new { subCategoryId = id }) ?? "",
                        "change-active-status",
                        "PATCH"
                    )
                );
                links.Add(
                    new LinkDto(
                        GetUriByAction(nameof(SubCategoryController.RemoveImageAsync), new { subCategoryId = id, imageId = 0 }) ?? "",
                        "remove-image",
                        "DELETE"
                    )
                );
            }
            return links;
        }
    }
} 