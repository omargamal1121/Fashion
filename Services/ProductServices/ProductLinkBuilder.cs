using E_Commerce.Controllers;
using E_Commerce.DtoModels.Shared;
using E_Commerce.Interfaces;
using E_Commerce.Services.Category;

namespace E_Commerce.Services.ProductServices
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
               
               
            }
            return links;
        }
    }
}
