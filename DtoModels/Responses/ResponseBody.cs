﻿using E_Commers.DtoModels.Shared;
using E_Commers.ErrorHnadling;
using System.Collections.Generic;

namespace E_Commers.DtoModels.Responses
{
	public class ResponseBody<T> 
	{
		public ErrorResponse? Errors { get; set; }
		public List<string>? Warnings { get; set; }
		public string? Message { get; set; }
		public T? Data { get; set; }
		public List<LinkDto>? Links { get; set; }
		
		public ResponseBody()
		{
		}
		
		public ResponseBody(ErrorResponse? error = null, string? message = null, T? data = default, List<LinkDto>? links = null, ErrorResponse? errorResponse = null,List<string>? warings=null)
		{
			Errors = error;
			Message = message;
			Data = data;
			Links = links;
			Warnings = warings;
		}
		
		
	}
}
