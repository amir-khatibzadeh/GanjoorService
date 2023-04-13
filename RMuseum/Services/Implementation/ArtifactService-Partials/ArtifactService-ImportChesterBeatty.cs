﻿using Microsoft.EntityFrameworkCore;
using RMuseum.DbContext;
using RMuseum.Models.Artifact;
using RMuseum.Models.ImportJob;
using RSecurityBackend.Models.Generic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RMuseum.Services.Implementation
{

    /// <summary>
    /// IArtifactService implementation
    /// </summary>
    public partial class ArtifactService : IArtifactService
    {
        /// <summary>
        /// import from https://viewer.cbl.ie
        /// </summary>
        /// <param name="resourceNumber">119</param>
        /// <param name="friendlyUrl">golestan-baysonghori</param>
        /// <returns></returns>
        private async Task<RServiceResult<bool>> StartImportingFromChesterBeatty(string resourceNumber, string friendlyUrl)
        {
            try
            {
                string srcUrl = $"https://viewer.cbl.ie/viewer/object/Per_{resourceNumber}/1/";
                if (
                    (
                    await _context.ImportJobs
                        .Where(j => j.JobType == JobType.ChesterBeatty && j.ResourceNumber == resourceNumber && !(j.Status == ImportJobStatus.Failed || j.Status == ImportJobStatus.Aborted))
                        .SingleOrDefaultAsync()
                    )
                    !=
                    null
                    )
                {
                    return new RServiceResult<bool>(false, $"Job is already scheduled or running for importing {srcUrl}");
                }

                if (string.IsNullOrEmpty(friendlyUrl))
                {
                    return new RServiceResult<bool>(false, $"Friendly url is empty, server folder {srcUrl}");
                }

                if (
                (await _context.Artifacts.Where(a => a.FriendlyUrl == friendlyUrl).SingleOrDefaultAsync())
                !=
                null
                )
                {
                    return new RServiceResult<bool>(false, $"duplicated friendly url '{friendlyUrl}'");
                }

                ImportJob job = new ImportJob()
                {
                    JobType = JobType.ChesterBeatty,
                    ResourceNumber = resourceNumber,
                    FriendlyUrl = friendlyUrl,
                    SrcUrl = srcUrl,
                    QueueTime = DateTime.Now,
                    ProgressPercent = 0,
                    Status = ImportJobStatus.NotStarted
                };


                await _context.ImportJobs.AddAsync
                    (
                    job
                    );

                await _context.SaveChangesAsync();

                _backgroundTaskQueue.QueueBackgroundWorkItem
                    (
                        async token =>
                        {
                            try
                            {
                                using (RMuseumDbContext context = new RMuseumDbContext(new DbContextOptions<RMuseumDbContext>()))
                                {
                                    RArtifactMasterRecord book = new RArtifactMasterRecord($"extracted from url {job.ResourceNumber}", $"extracted from url {job.ResourceNumber}")
                                    {
                                        Status = PublishStatus.Draft,
                                        DateTime = DateTime.Now,
                                        LastModified = DateTime.Now,
                                        CoverItemIndex = 0,
                                        FriendlyUrl = friendlyUrl,
                                    };


                                    List<RTagValue> meta = new List<RTagValue>();
                                    RTagValue tag;


                                    tag = await TagHandler.PrepareAttribute(context, "Type", "Book", 1);
                                    meta.Add(tag);

                                    tag = await TagHandler.PrepareAttribute(context, "Type", "Manuscript", 1);
                                    meta.Add(tag);

                                    tag = await TagHandler.PrepareAttribute(context, "Source", "Chester Beatty Digital Collections", 1);
                                    tag.ValueSupplement = srcUrl;

                                    meta.Add(tag);



                                    using (RMuseumDbContext importJobUpdaterDb = new RMuseumDbContext(new DbContextOptions<RMuseumDbContext>()))
                                    {
                                        job.StartTime = DateTime.Now;
                                        job.Status = ImportJobStatus.Running;
                                        job.SrcContent = "";
                                        importJobUpdaterDb.Update(job);
                                        await importJobUpdaterDb.SaveChangesAsync();
                                    }

                                    List<RArtifactItemRecord> pages = new List<RArtifactItemRecord>();
                                    int order = 0;
                                    using (var client = new HttpClient())
                                        do
                                        {


                                            order++;

                                            using (RMuseumDbContext importJobUpdaterDb = new RMuseumDbContext(new DbContextOptions<RMuseumDbContext>()))
                                            {
                                                job.ProgressPercent = order;
                                                importJobUpdaterDb.Update(job);
                                                await importJobUpdaterDb.SaveChangesAsync();
                                            }

                                            RArtifactItemRecord page = new RArtifactItemRecord()
                                            {
                                                Name = $"تصویر {order}",
                                                NameInEnglish = $"Image {order} of {book.NameInEnglish}",
                                                Description = "",
                                                DescriptionInEnglish = "",
                                                Order = order,
                                                FriendlyUrl = $"p{$"{order}".PadLeft(4, '0')}",
                                                LastModified = DateTime.Now
                                            };

                                            //string imageUrl = $"https://viewer.cbl.ie/viewer/rest/image/Per_{resourceNumber}/Per{resourceNumber}_{$"{order}".PadLeft(3, '0')}.jpg/full/!10000,10000/0/default.jpg?ignoreWatermark=true";
                                            string imageUrl = $"https://viewer.cbl.ie/viewer/api/v1/records/Per_{resourceNumber}/files/images/Per{resourceNumber}_{$"{order}".PadLeft(2, '0')}.jpg/full/max/0/default.jpg";

                                            page.Tags = new RTagValue[] { };

                                            bool recovered = false;

                                            if (
                                                           File.Exists
                                                           (
                                                           Path.Combine(Path.Combine(Path.Combine(_pictureFileService.ImageStoragePath, friendlyUrl), "orig"), $"{order}".PadLeft(4, '0') + ".jpg")
                                                           )
                                                           &&
                                                           File.Exists
                                                           (
                                                           Path.Combine(Path.Combine(Path.Combine(_pictureFileService.ImageStoragePath, friendlyUrl), "norm"), $"{order}".PadLeft(4, '0') + ".jpg")
                                                           )
                                                           &&
                                                           File.Exists
                                                           (
                                                           Path.Combine(Path.Combine(Path.Combine(_pictureFileService.ImageStoragePath, friendlyUrl), "thumb"), $"{order}".PadLeft(4, '0') + ".jpg")
                                                           )
                                                           )
                                            {
                                                RServiceResult<RPictureFile> picture = await _pictureFileService.RecoverFromeFiles(page.Name, page.Description, 1,
                                                    imageUrl,
                                                    Path.Combine(Path.Combine(Path.Combine(_pictureFileService.ImageStoragePath, friendlyUrl), "orig"), $"{order}".PadLeft(4, '0') + ".jpg"),
                                                    Path.Combine(Path.Combine(Path.Combine(_pictureFileService.ImageStoragePath, friendlyUrl), "norm"), $"{order}".PadLeft(4, '0') + ".jpg"),
                                                    Path.Combine(Path.Combine(Path.Combine(_pictureFileService.ImageStoragePath, friendlyUrl), "thumb"), $"{order}".PadLeft(4, '0') + ".jpg"),
                                                    $"{order}".PadLeft(4, '0') + ".jpg", friendlyUrl);
                                                if (picture.Result != null)
                                                {
                                                    recovered = true;
                                                    page.Images = new RPictureFile[] { picture.Result };
                                                    page.CoverImageIndex = 0;

                                                    if (book.CoverItemIndex == (order - 1))
                                                    {
                                                        book.CoverImage = RPictureFile.Duplicate(picture.Result);
                                                    }

                                                    tag = await TagHandler.PrepareAttribute(context, "Source", "Chester Beatty Digital Collections", 1);
                                                    tag.ValueSupplement = $"https://viewer.cbl.ie/viewer/object/Per_{resourceNumber}/{$"{order}".PadLeft(3, '0')}/"; ;
                                                    page.Tags = new RTagValue[] { tag };

                                                    pages.Add(page);
                                                }

                                            }

                                            if (!recovered)
                                            {
                                                if (
                                            File.Exists
                                            (
                                            Path.Combine(Path.Combine(Path.Combine(_pictureFileService.ImageStoragePath, friendlyUrl), "orig"), $"{order}".PadLeft(4, '0') + ".jpg")
                                            )

                                                               )
                                                {
                                                    File.Delete
                                                   (
                                                   Path.Combine(Path.Combine(Path.Combine(_pictureFileService.ImageStoragePath, friendlyUrl), "orig"), $"{order}".PadLeft(4, '0') + ".jpg")
                                                   );
                                                }
                                                if (

                                                   File.Exists
                                                   (
                                                   Path.Combine(Path.Combine(Path.Combine(_pictureFileService.ImageStoragePath, friendlyUrl), "norm"), $"{order}".PadLeft(4, '0') + ".jpg")
                                                   )

                                               )
                                                {
                                                    File.Delete
                                                    (
                                                    Path.Combine(Path.Combine(Path.Combine(_pictureFileService.ImageStoragePath, friendlyUrl), "norm"), $"{order}".PadLeft(4, '0') + ".jpg")
                                                    );
                                                }
                                                if (

                                                   File.Exists
                                                   (
                                                   Path.Combine(Path.Combine(Path.Combine(_pictureFileService.ImageStoragePath, friendlyUrl), "thumb"), $"{order}".PadLeft(4, '0') + ".jpg")
                                                   )
                                               )
                                                {
                                                    File.Delete
                                                    (
                                                    Path.Combine(Path.Combine(Path.Combine(_pictureFileService.ImageStoragePath, friendlyUrl), "thumb"), $"{order}".PadLeft(4, '0') + ".jpg")
                                                    );
                                                }
                                                var imageResult = await client.GetAsync(imageUrl);

                                                if (imageResult.StatusCode == HttpStatusCode.Forbidden || imageResult.StatusCode == HttpStatusCode.NotFound)
                                                    break;


                                                int _ImportRetryCount = 5;
                                                int _ImportRetryInitialSleep = 500;
                                                int retryCount = 0;
                                                while (retryCount < _ImportRetryCount && !imageResult.IsSuccessStatusCode && imageResult.StatusCode == HttpStatusCode.ServiceUnavailable)
                                                {
                                                    imageResult.Dispose();
                                                    Thread.Sleep(_ImportRetryInitialSleep * (retryCount + 1));
                                                    imageResult = await client.GetAsync(imageUrl);
                                                    retryCount++;
                                                }

                                                if (imageResult.IsSuccessStatusCode)
                                                {
                                                    using (Stream imageStream = await imageResult.Content.ReadAsStreamAsync())
                                                    {
                                                        RServiceResult<RPictureFile> picture = await _pictureFileService.Add(page.Name, page.Description, 1, null, imageUrl, imageStream, $"{order}".PadLeft(4, '0') + ".jpg", friendlyUrl);
                                                        if (picture.Result == null)
                                                        {
                                                            throw new Exception($"_pictureFileService.Add : {picture.ExceptionString}");
                                                        }

                                                        page.Images = new RPictureFile[] { picture.Result };
                                                        page.CoverImageIndex = 0;

                                                        if (book.CoverItemIndex == (order - 1))
                                                        {
                                                            book.CoverImage = RPictureFile.Duplicate(picture.Result);
                                                        }
                                                        tag = await TagHandler.PrepareAttribute(context, "Source", "Chester Beatty Digital Collections", 1);
                                                        tag.ValueSupplement = $"https://viewer.cbl.ie/viewer/object/Per_{resourceNumber}/{$"{order}".PadLeft(3, '0')}/";
                                                        page.Tags = new RTagValue[] { tag };

                                                        pages.Add(page);
                                                    }
                                                }
                                                else
                                                {
                                                    using (RMuseumDbContext importJobUpdaterDb = new RMuseumDbContext(new DbContextOptions<RMuseumDbContext>()))
                                                    {
                                                        job.EndTime = DateTime.Now;
                                                        job.Status = ImportJobStatus.Failed;
                                                        job.Exception = $"Http result is not ok ({imageResult.StatusCode}) for page {order}, url {imageUrl}";
                                                        importJobUpdaterDb.Update(job);
                                                        await importJobUpdaterDb.SaveChangesAsync();
                                                    }

                                                    imageResult.Dispose();
                                                    return;
                                                }


                                                imageResult.Dispose();
                                                GC.Collect();
                                            }


                                            pages.Add(page);
                                        }
                                        while (true);


                                    book.Tags = meta.ToArray();

                                    book.Items = pages.ToArray();
                                    book.ItemCount = pages.Count;

                                    if (pages.Count == 0)
                                    {
                                        using (RMuseumDbContext importJobUpdaterDb = new RMuseumDbContext(new DbContextOptions<RMuseumDbContext>()))
                                        {
                                            job.EndTime = DateTime.Now;
                                            job.Status = ImportJobStatus.Failed;
                                            job.Exception = "Pages.Count == 0";
                                            importJobUpdaterDb.Update(job);
                                            await importJobUpdaterDb.SaveChangesAsync();
                                        }
                                        return;
                                    }

                                    await context.Artifacts.AddAsync(book);
                                    await context.SaveChangesAsync();

                                    var resFTPUpload = await _UploadArtifactToExternalServer(book, context);
                                    if (!string.IsNullOrEmpty(resFTPUpload.ExceptionString))
                                    {
                                        job.EndTime = DateTime.Now;
                                        job.Status = ImportJobStatus.Failed;
                                        job.Exception = $"UploadArtifactToExternalServer: {resFTPUpload.ExceptionString}";
                                        job.ArtifactId = book.Id;
                                        job.EndTime = DateTime.Now;
                                        context.Update(job);
                                        await context.SaveChangesAsync();
                                        return;
                                    }

                                    job.ProgressPercent = 100;
                                    job.Status = ImportJobStatus.Succeeded;
                                    job.ArtifactId = book.Id;
                                    job.EndTime = DateTime.Now;
                                    context.Update(job);
                                    await context.SaveChangesAsync();



                                }
                            }
                            catch (Exception exp)
                            {
                                using (RMuseumDbContext importJobUpdaterDb = new RMuseumDbContext(new DbContextOptions<RMuseumDbContext>()))
                                {
                                    job.EndTime = DateTime.Now;
                                    job.Status = ImportJobStatus.Failed;
                                    job.Exception = exp.ToString();
                                    importJobUpdaterDb.Update(job);
                                    await importJobUpdaterDb.SaveChangesAsync();
                                }

                            }
                        }
                    );

                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }
    }
}
