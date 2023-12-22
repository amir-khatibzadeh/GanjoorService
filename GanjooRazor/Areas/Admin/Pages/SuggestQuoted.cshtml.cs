﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using RMuseum.Models.Ganjoor.ViewModels;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using RMuseum.Models.Ganjoor;
using GanjooRazor.Utils;
using System.Text;
using System.Linq;
using System.Net;

namespace GanjooRazor.Areas.Admin.Pages
{
    [IgnoreAntiforgeryToken(Order = 1001)]
    public class SuggestQuotedModel : PageModel
    {
        public string LastMessage { get; set; }

        /// <summary>
        /// HttpClient instance
        /// </summary>
        private readonly HttpClient _httpClient;

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="httpClient"></param>
        public SuggestQuotedModel(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public GanjoorPoemCompleteViewModel Poem { get; set; }

        public GanjoorPoemCompleteViewModel RelatedPoem { get; set; }

        public Tuple<int, string>[] Couplets { get; set; }

        public Tuple<int, string>[] RelatedCouplets { get; set; }

        [BindProperty]
        public GanjoorQuotedPoem GanjoorQuotedPoem { get; set; }

        private Tuple<int, string>[] GetCouplets(GanjoorVerseViewModel[] verses)
        {
            int coupetIndex = -1;
            string coupletText = "";
            List<Tuple<int, string>> couplets = new List<Tuple<int, string>>();
            int verseIndex = 0;
            bool incompleteCouplet = false;
            while (verseIndex < verses.Length)
            {
                switch (verses[verseIndex].VersePosition)
                {
                    case VersePosition.Comment:
                        incompleteCouplet = false;
                        if (!string.IsNullOrEmpty(coupletText))
                        {
                            couplets.Add(new Tuple<int, string>(coupetIndex, coupletText));
                            coupletText = "";
                        }
                        break;
                    case VersePosition.Paragraph:
                    case VersePosition.Single:
                        incompleteCouplet = false;
                        if (!string.IsNullOrEmpty(coupletText))
                        {
                            couplets.Add(new Tuple<int, string>(coupetIndex, coupletText));
                            coupletText = "";
                        }
                        coupetIndex++;
                        couplets.Add(new Tuple<int, string>(coupetIndex, verses[verseIndex].Text));
                        break;
                    case VersePosition.Right:
                    case VersePosition.CenteredVerse1:
                        incompleteCouplet = false;
                        if (!string.IsNullOrEmpty(coupletText))
                        {
                            couplets.Add(new Tuple<int, string>(coupetIndex, coupletText));
                        }
                        coupetIndex++;
                        coupletText = verses[verseIndex].Text;
                        break;
                    case VersePosition.Left:
                    case VersePosition.CenteredVerse2:
                        incompleteCouplet = true;
                        coupletText += $" - {verses[verseIndex].Text}";
                        break;
                }
                verseIndex++;
            }


            if (incompleteCouplet && !string.IsNullOrEmpty(coupletText))
                couplets.Add(new Tuple<int, string>(coupetIndex, coupletText));

            return couplets.ToArray();
        }

        private async Task<string> Prepare(int poemId, string id)
        {
            LastMessage = "";
            var poemQuery = await _httpClient.GetAsync($"{APIRoot.Url}/api/ganjoor/poem/{poemId}");
            if (!poemQuery.IsSuccessStatusCode)
            {
                LastMessage = JsonConvert.DeserializeObject<string>(await poemQuery.Content.ReadAsStringAsync());
                return LastMessage;
            }
            Poem = JObject.Parse(await poemQuery.Content.ReadAsStringAsync()).ToObject<GanjoorPoemCompleteViewModel>();

            Couplets = GetCouplets(Poem.Verses);
            RelatedCouplets = [];

            if (!string.IsNullOrEmpty(id))
            {
              
                var quoteQuery = await _httpClient.GetAsync($"{APIRoot.Url}/api/ganjoor/quoted/{id}");
                if (!quoteQuery.IsSuccessStatusCode)
                {
                    LastMessage = JsonConvert.DeserializeObject<string>(await quoteQuery.Content.ReadAsStringAsync());
                    return LastMessage;
                }
                GanjoorQuotedPoem = JObject.Parse(await quoteQuery.Content.ReadAsStringAsync()).ToObject<GanjoorQuotedPoem>();
                if (GanjoorQuotedPoem.RelatedPoemId != null)
                {
                    var relPoemQuery = await _httpClient.GetAsync($"{APIRoot.Url}/api/ganjoor/poem/{GanjoorQuotedPoem.RelatedPoemId}");
                    if (!relPoemQuery.IsSuccessStatusCode)
                    {
                        LastMessage = JsonConvert.DeserializeObject<string>(await relPoemQuery.Content.ReadAsStringAsync());
                        return LastMessage;
                    }
                    RelatedPoem = JObject.Parse(await relPoemQuery.Content.ReadAsStringAsync()).ToObject<GanjoorPoemCompleteViewModel>();

                    RelatedCouplets = GetCouplets(RelatedPoem.Verses);
                }

            }
            else
            {
                GanjoorQuotedPoem = new GanjoorQuotedPoem()
                {
                    PoemId = poemId,
                    PoetId = Poem.Category.Poet.Id,
                    RelatedPoetId = null,
                    RelatedPoemId = null,
                    IsPriorToRelated = false,
                    ChosenForMainList = true,
                    CachedRelatedPoemPoetDeathYearInLHijri = 0,
                    CachedRelatedPoemPoetName = null,
                    CachedRelatedPoemPoetUrl = null,
                    CachedRelatedPoemPoetImage = null,
                    CachedRelatedPoemFullTitle = null,
                    CachedRelatedPoemFullUrl = null,
                    SortOrder = 1000,
                    Note = "",
                    Published = false,
                    ClaimedByBothPoets = false,
                    IndirectQuotation = false,
                    SamePoemsQuotedCount = 0,
                    RelatedCoupletVerse1 = null,
                    RelatedCoupletVerse1ShouldBeEmphasized = false,
                    RelatedCoupletVerse2 = null,
                    RelatedCoupletVerse2ShouldBeEmphasized = false,
                    RelatedCoupletIndex = null,
                    CoupletVerse1 = Poem.Verses[0].Text,
                    CoupletVerse1ShouldBeEmphasized = false,
                    CoupletVerse2 = Poem.Verses[1].Text,
                    CoupletVerse2ShouldBeEmphasized = false,
                    CoupletIndex = 0,

                };
            }
            return LastMessage;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrEmpty(Request.Cookies["Token"]))
                return Redirect("/");

            if (string.IsNullOrEmpty(Request.Query["p"]))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "شناسهٔ شعر مشخص نیست.");
            }

            await Prepare(int.Parse(Request.Query["p"]), Request.Query["id"]);



            return Page();
        }

        public async Task<IActionResult> OnPostAsync(GanjoorQuotedPoem GanjoorQuotedPoem)
        {
            await Prepare(GanjoorQuotedPoem.PoemId, GanjoorQuotedPoem.Id == Guid.Empty ? null : GanjoorQuotedPoem.Id.ToString());
            GanjoorQuotedPoem.CoupletVerse1 = Poem.Verses.Where(v => v.CoupletIndex == GanjoorQuotedPoem.CoupletIndex).ToArray()[0].Text;
            GanjoorQuotedPoem.CoupletVerse2 = Poem.Verses.Where(v => v.CoupletIndex == GanjoorQuotedPoem.CoupletIndex).ToArray()[1].Text;
            if (GanjoorQuotedPoem.RelatedPoemId != null && RelatedPoem == null)
            {
                var relPoemQuery = await _httpClient.GetAsync($"{APIRoot.Url}/api/ganjoor/poem/{GanjoorQuotedPoem.RelatedPoemId}");
                if (!relPoemQuery.IsSuccessStatusCode)
                {
                    LastMessage = JsonConvert.DeserializeObject<string>(await relPoemQuery.Content.ReadAsStringAsync());
                    return Page();
                }
                RelatedPoem = JObject.Parse(await relPoemQuery.Content.ReadAsStringAsync()).ToObject<GanjoorPoemCompleteViewModel>();

                RelatedCouplets = GetCouplets(RelatedPoem.Verses);
            }

            if (GanjoorQuotedPoem.RelatedCoupletIndex != null)
            {
                GanjoorQuotedPoem.RelatedCoupletVerse1 = RelatedPoem.Verses.Where(v => v.CoupletIndex == GanjoorQuotedPoem.RelatedCoupletIndex).ToArray()[0].Text;
                GanjoorQuotedPoem.RelatedCoupletVerse2 = RelatedPoem.Verses.Where(v => v.CoupletIndex == GanjoorQuotedPoem.RelatedCoupletIndex).ToArray()[1].Text;
            }
            using (HttpClient secureClient = new HttpClient())
            {
                if (await GanjoorSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    var url = $"{APIRoot.Url}/api/ganjoor/quoted";
                    var payload = new StringContent(JsonConvert.SerializeObject(GanjoorQuotedPoem), Encoding.UTF8, "application/json");
                    bool newRecord = GanjoorQuotedPoem.Id == Guid.Empty;
                    HttpResponseMessage response = 
                        newRecord ?
                        await secureClient.PostAsync(url, payload) :
                        await secureClient.PutAsync(url, payload);
                    if (!response.IsSuccessStatusCode)
                    {
                        LastMessage = JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync());
                    }
                    else
                    {
                        if(newRecord)
                        {
                            GanjoorQuotedPoem = JObject.Parse(await response.Content.ReadAsStringAsync()).ToObject<GanjoorQuotedPoem>();
                        }
                        
                        LastMessage = $"انجام شد. <br /><a href=\"/Admin/SuggestQuoted/?p={GanjoorQuotedPoem.PoemId}&id={GanjoorQuotedPoem.Id}\">برگشت</a>";
                        
                    }
                }
                else
                {
                    LastMessage = "لطفاً از گنجور خارج و مجددا به آن وارد شوید.";
                }

            }
            return Page();
        }

        public async Task<IActionResult> OnDeleteAsync(string id)
        {
            using (HttpClient secureClient = new HttpClient())
            {
                if (await GanjoorSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    var response = await secureClient.DeleteAsync($"{APIRoot.Url}/api/ganjoor/quoted?id={id}");

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        return new BadRequestObjectResult(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }

                }
                else
                {
                    return new BadRequestObjectResult("لطفاً از گنجور خارج و مجددا به آن وارد شوید.");
                }
            }
            return new JsonResult(true);
        }
    }
}
