using E_Commers.Controllers;
using E_Commers.DtoModels.Shared;
using E_Commers.Interfaces;
using E_Commers.Services.Category;

namespace E_Commers.Services.ProductServices
{
    public class ProductLinkBuilder : BaseLinkBuilder,IProductLinkBuilder
    {
        protected override string ControllerName => "Products";
      


		public ProductLinkBuilder(IHttpContextAccessor context, LinkGenerator generator)
            : base(context, generator) 
        {
    

        }

        public override List<LinkDto> GenerateLinks(int? id = null)
        {
            if (_context.HttpContext == null)
                return new List<LinkDto>();

            var links = new List<LinkDto>
            {
                new LinkDto(
                    GetUriByAction(nameof(ProductController.CreateProduct)) ?? "",
                    "create",
                    "POST"
                ),
                new LinkDto(
                    GetUriByAction(nameof(ProductController.GetBestSellers)) ?? "",
                    "bestsellers",
                    "GET"
                ),
                new LinkDto(
                    GetUriByAction(nameof(ProductController.GetNewArrivals)) ?? "",
                    "newarrivals",
                    "GET"
                ),
                new LinkDto(
                    GetUriByAction(nameof(ProductController.AdvancedSearch)) ?? "",
                    "advanced-search",
                    "POST"
                )
            };

            if (id != null)
            {
                links.Add(new LinkDto(
                    GetUriByAction(nameof(ProductController.GetProductPublic), new { id }) ?? "",
                    "get-by-id",
                    "GET"
                ));
                links.Add(new LinkDto(
                    GetUriByAction(nameof(ProductController.UpdateProduct), new { id }) ?? "",
                    "update",
                    "PUT"
                ));
                links.Add(new LinkDto(
                    GetUriByAction(nameof(ProductController.DeleteProduct), new { id }) ?? "",
                    "delete",
                    "DELETE"
                ));
                links.Add(new LinkDto(
                    GetUriByAction(nameof(ProductController.GetProductImages), new { id }) ?? "",
                    "get-images",
                    "GET"
                ));
                links.Add(new LinkDto(
                    GetUriByAction(nameof(ProductController.AddProductImages), new { id }) ?? "",
                    "add-images",
                    "POST"
                ));
                links.Add(new LinkDto(
                    GetUriByAction(nameof(ProductController.UploadAndSetMainImage), new { id }) ?? "",
                    "set-main-image",
                    "POST"
                ));
                links.Add(new LinkDto(
                    GetUriByAction(nameof(ProductController.GetProductVariants), new { id }) ?? "",
                    "get-variants",
                    "GET"
                ));
               
                links.Add(new LinkDto(
                    GetUriByAction(nameof(ProductController.GetProductDiscount), new { id }) ?? "",
                    "get-discount",
                    "GET"
                ));
                links.Add(new LinkDto(
                    GetUriByAction(nameof(ProductController.AddDiscountToProduct), new { id }) ?? "",
                    "add-discount",
                    "POST"
                ));
                links.Add(new LinkDto(
                    GetUriByAction(nameof(ProductController.UpdateProductDiscount), new { id }) ?? "",
                    "update-discount",
                    "PUT"
                ));
                links.Add(new LinkDto(
                    GetUriByAction(nameof(ProductController.RemoveDiscountFromProduct), new { id }) ?? "",
                    "remove-discount",
                    "DELETE"
                ));
            }
            return links;
        }
    }
}
