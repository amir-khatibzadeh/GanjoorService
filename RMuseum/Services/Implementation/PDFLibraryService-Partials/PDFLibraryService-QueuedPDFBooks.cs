﻿using ganjoor;
using Microsoft.EntityFrameworkCore;
using RMuseum.DbContext;
using RMuseum.Models.Artifact;
using RMuseum.Models.ImportJob;
using RMuseum.Models.PDFLibrary;
using RSecurityBackend.Models.Generic;
using RSecurityBackend.Services.Implementation;
using System;
using System.Linq;
using System.Threading.Tasks;
namespace RMuseum.Services.Implementation
{
    public partial class PDFLibraryService
    {
        /// <summary>
        /// queued downloding pdf books
        /// </summary>
        /// <param name="paging"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, QueuedPDFBook[] Books)>> GetQueuedPDFBooksAsync(PagingParameterModel paging)
        {
            try
            {
                var source =
                _context.QueuedPDFBooks.AsNoTracking()
               .OrderBy(t => t.Id)
               .AsQueryable();
                (PaginationMetadata PagingMeta, QueuedPDFBook[] Books) paginatedResult =
                    await QueryablePaginator<QueuedPDFBook>.Paginate(source, paging);
               
                return new RServiceResult<(PaginationMetadata PagingMeta, QueuedPDFBook[] Books)>(paginatedResult);
            }
            catch (Exception exp)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, QueuedPDFBook[] Books)>((null, null), exp.ToString());
            }
        }

        /// <summary>
        /// delete queued books
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> DeleteQueuedPDFBookAsync(Guid id)
        {
            try
            {
                var qb = await _context.QueuedPDFBooks.Where(t => t.Id == id).SingleAsync();
                _context.Remove(qb);
                await _context.SaveChangesAsync();
                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }
    }
}
