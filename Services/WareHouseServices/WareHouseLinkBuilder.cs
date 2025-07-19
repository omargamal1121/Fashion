﻿using E_Commers.Controllers;
using E_Commers.DtoModels.Shared;
using E_Commers.Interfaces;
using E_Commers.Services.Category;

namespace E_Commers.Services.WareHouseServices
{
	public class WareHouseLinkBuilder : BaseLinkBuilder, IWareHouseLinkBuilder
	{
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly LinkGenerator _linkGenerator;
		public WareHouseLinkBuilder(IHttpContextAccessor httpContextAccessor,LinkGenerator linkGenerator ):base(httpContextAccessor,linkGenerator)
		{
			_httpContextAccessor = httpContextAccessor;
			_linkGenerator = linkGenerator;
			
		}
		protected override string ControllerName => nameof(WareHousesController).Replace("Controller","");

		public override List<LinkDto> GenerateLinks(int? id = null)
		{
			var list = new List<LinkDto>
			{
					new LinkDto(GetUriByAction(nameof(WareHousesController.CreateWareHouseAsync))??"","Create-WareHouse","POST"),
				new LinkDto(GetUriByAction(nameof(WareHousesController.GetAll))??"","get-all-WareHouse","GET"),
			
			};
			if (id != null)
				list.AddRange(new LinkDto(GetUriByAction(nameof(WareHousesController.GetProductsByWareHouseId), id) ?? "", "get-WareHouse-product", "GET"),
				new LinkDto(GetUriByAction(nameof(WareHousesController.ReturnRemovedWareHouseAsync), id) ?? "", "return-removed-warehouse", "PATCH"), 
				new LinkDto(GetUriByAction(nameof(WareHousesController.DeleteWareHouseAsync), id) ?? "", "delete-WareHouse", "DELETE"),
				new LinkDto(GetUriByAction(nameof(WareHousesController.UpdateWareHouseAsync), id) ?? "", "update-WareHouse", "PUT"));
			return list;
		}
	}
}
