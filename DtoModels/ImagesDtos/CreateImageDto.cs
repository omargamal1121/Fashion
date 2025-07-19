using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace E_Commers.DtoModels.ImagesDtos
{
	public class CreateImageDto
	{
		[Required]
		public List<IFormFile> Files { get; set; } = new List<IFormFile>();
	}
} 