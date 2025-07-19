﻿using E_Commers.Context;

namespace E_Commers.BackgroundJops
{
	public class CategoryCleanupService
	{
		private readonly AppDbContext _context;

		public CategoryCleanupService(AppDbContext context)
		{
			_context = context;
		}

		public void DeleteOldCategories()
		{
			var now = DateTime.UtcNow;
			var categoriesToDelete = _context.Categories
				.Where(c =>c.DeletedAt != null )
				.ToList();

			if (categoriesToDelete.Any())
			{
				_context.Categories.RemoveRange(categoriesToDelete);
				_context.SaveChanges();
				Console.WriteLine($"🗑️Number Of Deleted Category:{categoriesToDelete.Count}");
			}
		}
	}
}
