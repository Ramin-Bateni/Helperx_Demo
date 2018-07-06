using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using AutoMapper;
using Helperx.Common;
using Helperx.Common.Exceptions;
using Helperx.Common.ExceptionsAndReplyMessages;
using Helperx.Common.Filters;
using Helperx.Common.Helpers;
using Helperx.Common.Helpers.Extentions;
using Helperx.DataLayer.Context;
using Helperx.DomainClasses.Entities.Common;
using Helperx.DomainClasses.Entities.Common.Enums;
using Helperx.DomainClasses.Entities.SocialNetworks;
using Helperx.DomainClasses.Entities.SocialNetworks.Contents;
using Helperx.DomainClasses.OtherModels;
using Helperx.Resource;
using Helperx.ServiceLayer.Contracts;
using Helperx.ServiceLayer.EFServiecs.Common;
using Helperx.ServiceLayer.Inf;
using Helperx.Utility;
using Helperx.ViewModel.Areas.ControlPanel.Posts;
using Helperx.Web.Controllers.Common;
using Helperx.Web.Extentions;
using Helperx.Web.Filters;

namespace Helperx.Web.Areas.CP.Controllers
{

    //  [RouteArea("CP")]
    // [RoutePrefix("Post")]
    //[Route("{action}")]
    [LangOverride]
    [Mcv5Authorize(Roles = HxRoles.PLAN_TRIAL + "," + HxRoles.PLAN_BASIC + "," + HxRoles.PLAN_STANDARD + "," + HxRoles.PLAN_PLUS + "," + HxRoles.PLAN_PRO + "," + HxRoles.PLAN_ULTIMATE)]
    [ValidateAntiForgeryTokenOnAllPosts]
    public partial class PlannerController : BaseController
    {
        private readonly IThemeSettingService _themeSettingService;
        private readonly IPostService _postService;
        private readonly IContentPackService _contentPackService;
        private readonly IContentManager _contentManager;
        private readonly IPortAdminService _portAdminService;
        private readonly IBrandService _brandService;
        private readonly IUnitOfWork _uow;
        private readonly IConsumptionManager _consManager;
        //     private readonly IPortService _portService;
        //private readonly IEntityInstanceServTypeService _instanceServTypeService;
        //   private IApplicationUserManager _userManager;
        //     private readonly IMappingEngine _mappingEngine;
        //     private readonly ISocialNetworkService _socialNetworkService;
        private const int LIMIT_MAX_POST_IN_EACH_EVENT_FOR_TELEGRAM_PORT = 20;

        public PlannerController(
            IThemeSettingService themeSettingService,
            IPostService postService,
            IContentPackService contentPackService,
            IContentManager contentManager,
            IUnitOfWork uow,
            IPortAdminService portAdminService,
            IBrandService brandService,
            IConsumptionManager consManager
            //IMappingEngine mappingEngine, ISocialNetworkService socialNetworkService , IApplicationUserManager userManager, IPortService portService,IEntityInstanceServTypeService instanceServTypeService,
        )
        {
            _themeSettingService = themeSettingService;
            _postService = postService;
            _contentPackService = contentPackService;
            _contentManager = contentManager;
            _portAdminService = portAdminService;
            _brandService = brandService;
            _consManager = consManager;
            _uow = uow;
            //  _socialNetworkService = socialNetworkService;
            //   _mappingEngine = mappingEngine;
            //   _portService = portService;
            //  _instanceServTypeService = instanceServTypeService;
            //    _userManager = userManager;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="strBrandGuid">BrandGuid</param>
        /// <returns></returns>
        //[ActionName("contents")]
        public virtual async Task<ActionResult> Index([Bind(Prefix = "bgid")] string strBrandGuid)
        {
            this.FillWelcomeViewModel(false);
            var errorCode = CodeOfMethods.WEB_SCHEDULE_CONTROLLER_LOAD_SECHDULED_CONTENTS.MakeErrorCode(CodeOfProjects.Web);
            var vModel = new BrandSelectList_ToSchedulingContentViewModel();
            int? brandId = null;
            try
            {
                //_______________________________________________________________________________________________________________________________
                //چک میکنیم ببینیم آیا شناسه جهانی ورودی فرمت معتبری دارد؟
                var brandGuid = new Guid();
                if (strBrandGuid != null && Guid.TryParse(strBrandGuid, out brandGuid))
                {
                    // اگر شناسه جهانی معتبری داریم برای واکشی برند آن تلاش میکنیم
                    if (brandGuid != Guid.Empty)
                    {
                        // لود برند
                        brandId = await _brandService.GetBrandId(brandGuid);
                        //todo برای بهینه کردن این متد بهتر است اگر به اینجا رسیدیم، اینجا کدی بنویسم که دسترسی کاربر را فقط راجع به این برند چک کند و کد زیر که دسترسی کاربر را نسبت به همه برندها درمیآورد و سپس دسترسی او را برای این برند بررسی میکند دیگر اجرا نشود چرا که کد زیر سنگین است
                    }
                }

                //_______________________________________________________________________________________________________________________________
                // اطلاعات برندهایی که کاربر مجاز به مدیریت آنهاست را واکشی میکنیم
                var accessedBrands =  _brandService.GetAllBrands_ThatThisUserHasAcceessToThem(CurUserId)
                    .Include(x => x.Ports)
                    .Select(x => new BrandItem_ToSchedulingContentViewModel
                    {
                        Title = x.Title,
                        BrandGuid = x.BrandGuid, //<< برای امنیت از جی‌یو‌آی‌دی بجای شناسه برند در ویو استفاده میکنم
                        BrandId = x.BrandId,
                        LogoSocialPortGuid = x.LogoSocialPortGuid,
                        BrandOwnerUserId = x.OwnerUserId.Value,
                        IsMyselfBrand = x.OwnerUserId==CurUserId,
                        // در سیستم ما همه برند‌های کاربر برای برنامه‌ریزی مجازند
                        // در مقابل این پورت‌ها هستند که ممکن است مجاز باشند یا نه
                        // که آن هم به تعداد کانال فعال در طرح و سرویس کانال اضافی خریداری شده
                        // و تنظیمات کاربر درخصوص کانال‌های فعال کاربر برای دریافت خدمات زمانبندی برمیگردد
                        ConsumeServiceStatus = ConsumeServiceStatus.AllowdService
                    })
                    .ToList();

                //_______________________________________________________________________________________________________________________________

                var allowedBrandsThatAsAtlastOneAssigedPortToSchedulingServ = _consManager.GetBrandIds_ThatAtlastOneOfItsPorts_HasActiveCurrentAssignPort_ToTheServTyp_InConsuptionse(accessedBrands.Select(x=>x.BrandId).ToList(), HxServTypes.Posting);
                // وضعیت تک تک برندهای کاربر را در مورد سرویس مربوطه بدست می‌آوریم
                // و نتایج را در ویومدل هم اضافه میکنیم
                // var consumeServiceStatus_Of_AccessedBrands = _portXrefServTypeService.GetConsumeServiceStatuse_ForeachPortInTheList_ForGivenService_Async(accessedBrands.Select(x => x.BrandId).ToList(), HxServTypes.Planning);

                foreach (var accessedBrand in accessedBrands)
                {
                    if (!allowedBrandsThatAsAtlastOneAssigedPortToSchedulingServ.Any())
                    {
                        accessedBrand.ConsumeServiceStatus = ConsumeServiceStatus.ThisPortHasNotAssignedToTheServType;
                    }
                    else
                    {
                        //ConsumeServiceStatus consumeServiceStatus;
                        //accessedBrand.ConsumeServiceStatus = consumeServiceStatus_Of_AccessedBrands.TryGetValue(accessedBrand.BrandId, out consumeServiceStatus) ? consumeServiceStatus : ConsumeServiceStatus.ThisPortHasNotAssignedToTheServType;
                        accessedBrand.ConsumeServiceStatus = allowedBrandsThatAsAtlastOneAssigedPortToSchedulingServ.Contains(accessedBrand.BrandId) ? ConsumeServiceStatus.AllowdService : ConsumeServiceStatus.ThisPortHasNotAssignedToTheServType;
                    }
                }
                vModel.AccessedBrands.AddRange(accessedBrands);
                //_______________________________________________________________________________________________________________________________
                //اگر برند واکشی شده ای داریم
                // چک میکنیم ببینی آیا در لیست برندهای قابل دسترس این کاربر هست و درضمن مجاز به مصرف این سرویس هست یا نه
                // اگر بود شناسه جهانی آن را به ویو میدهیم تا لودش را در دستورکار بگیرد
                if (true || brandId != null && accessedBrands.Where(x => x.ConsumeServiceStatus == ConsumeServiceStatus.AllowdService).Select(x => x.BrandId).Contains((int) brandId))
                    vModel.SelectedBrandGuid = brandGuid;

                //_______________________________________________________________________________________________________________________________
                // دیتای برگشتی را به عنوان یک بسته اطلاعات بدون خطا مارک میزنیم
                // لاگ خالی را به عنوان عدم وجود خطا برداشت خواهیم کرد
                vModel.Log = null;
                
                //vModel.Error=ScheduleContentsErrors.None;
            }
            catch (Exception ex)
            {
                vModel.Log = new ReplyMessageWrapper<ScheduleContentsErrors>(ScheduleContentsErrors.FailInGet_UserBrandList, HxException.Generate(ex, errorCode).HxErrorCode);
            }

            ViewBag.ThemeSetting = _themeSettingService.Get(UiPositions.ScheduleController);

            return View(MVC.CP.Planner.Views.Index,vModel);
        }

        #region Load Content Schedule
        //todo بررسی کنم که آیا این متد را از نوع پست کنم یا گت بماند
        [AjaxOnly]
        public virtual async Task<JsonResult> LoadSechduledContents([Bind(Prefix = "bgid")]string strBrandGuid)
        {
            var errorCode = CodeOfMethods.WEB_SCHEDULE_CONTROLLER_LOAD_SECHDULED_CONTENTS.MakeErrorCode(CodeOfProjects.Web);
            var schedullerData = new DtoScheduler_LoadDataPackViewModel();
            try
            {
                //------------------------------------------
                var brandGuid = new Guid(strBrandGuid);
                if (brandGuid == new Guid())
                {
                    schedullerData.Log.Add(new ReplyMessageWrapper<ScheduleContentsErrors>(Lang.SchedulingContent_Err_BrandNotFoundDuringSave_Lable, Lang.SchedulingContent_Err_BrandNotFoundDuringSave_Dest, errorCode));
                }
                else
                {
                    var brand = _brandService.GetBrand(brandGuid);

                    // بررسی میکنیم ببینیم آیا این کاربر اجازه دسترسی به اطلاعات برند مذکور دارد یا خیر
                    if (brand != null)
                    {
                        var servTerms = _consManager.GetBalanceOf_PostPlanningServ(brand.OwnerUserId.Value);
                        if (servTerms != null)
                        {
                            schedullerData = await FetchSchedulerData(errorCode, brand, servTerms);
                        }
                        else
                        {
                            schedullerData.Log.Add(new ReplyMessageWrapper<ScheduleContentsErrors>(Lang.SchedulingContent_Err_ServTermsIsNullDuringLoad_Lable, Lang.SchedulingContent_Err_ServTermsIsNullDuringLoad_Dest, errorCode));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                schedullerData.Log.Add(new ReplyMessageWrapper<ScheduleContentsErrors>(ScheduleContentsErrors.BrandAccessDeny, HxException.Generate(ex, errorCode).HxErrorCode));
            }

            return Json(schedullerData, JsonRequestBehavior.AllowGet);
        }

        /// <summary>
        /// این متد را بعد از اطمینان از مجاز بودن کاربر به دسترسی اطلاعات زمانبندی برای برند درخواستی صدا میزنیم
        /// و وظیفه آن ساخت یک ویومدل مناسب برای تبدیل به جیسان از اطلاعات زمانبندی‌های برند ورودی می‌باشد
        /// </summary>
        /// <param name="sendersErrorCode"></param>
        /// <param name="brand">شناسه برند</param>
        /// <param name="servTerms"></param>
        /// <returns></returns>
        [NonAction]
        private async Task<DtoScheduler_LoadDataPackViewModel> FetchSchedulerData(string sendersErrorCode, Brand brand, ServTerms servTerms)
        {
            var errorCode = CodeOfMethods.WEB_SCHEDULE_CONTROLLER_FETCH_SCHEDULER_DATA.MakeErrorCode(CodeOfProjects.Web,sendersErrorCode);
            var dtoScheduler = new DtoScheduler_LoadDataPackViewModel();//ScheduleContentsErrors.None);
            try
            {
                //----------- لیست رویدادها و آیتم‌های زمانبندی شده در هر رویداد-------------------------------------------------------------
                dtoScheduler.Log.Clear();
                dtoScheduler.Log.Add(new ReplyMessageWrapper<ScheduleContentsErrors>(ScheduleContentsErrors.FailInLoading_Schedules,errorCode));

                // تعیین میکنیم که برنامه از چه تاریخی در گذشته لود شود
                var fromDt = DateTime.Today - TimeSpan.FromDays(30);
                // لود برنامه زمانبندی برند درخواستی
                var itemsPack = await _postService.GetGroupedPostsAsync(brand.BrandId, fromDt, User.GetUserTimezoneId());
                dtoScheduler.EventItems = itemsPack.ToArray();

                
                //----------- لیست شبکه‌های اجتماعی که این برند پورت متناظری دارد در آن که مجاز به استفاده از سرویس زمانبندی است-----------
                dtoScheduler.Log.Clear();
                dtoScheduler.Log.Add(new ReplyMessageWrapper<ScheduleContentsErrors>(ScheduleContentsErrors.FailInLoading_SocialNetworks, errorCode));

                var socialNetworks = _consManager
                    .GetSocialNetworks_OfPortsOfBrand_ThaHave_ActiveCurrentPortAssing_ToTheServTyp_ToConsumption(brand.BrandId, HxServTypes.Posting)
                    .Select(network => new DtoScheduler_SocialNetworkViewModel
                    {
                        CssClass = network.CssClass,
                        SocialNetworkId = network.SocialNetworkId,
                        Title = Lang.ResourceManager.GetString(network.Title)
                    })
                    .ToArray();
                //var socialNetworks= _socialNetworkService.GetSocialNetworks(true).Select(network => new DtoScheduler_SocialNetworkViewModel()
                //{
                //    CssClass = network.CssClass,
                //    SocialNetworkId = network.SocialNetworkId,
                //    Title = Lang.ResourceManager.GetString(network.Title)
                //}).ToArray();
                dtoScheduler.DestItems = socialNetworks;

                //----------- تنظیمات پلاگین زمانبندی------------------------------------------------------------------------------------------
                dtoScheduler.Log.Clear();
                dtoScheduler.Log.Add(new ReplyMessageWrapper<ScheduleContentsErrors>(ScheduleContentsErrors.FailInLoading_ConfigureSettings, errorCode));

                var closestEventToNow = ClosestTimeTo(errorCode, DateTime.UtcNow, itemsPack.Select(x => x.EventDateTime).ToList())?.ConvertTimeFromUtcToUserTimezone(User);
                var maxEventDt = !itemsPack.Any() ? DateTime.UtcNow : itemsPack.DefaultIfEmpty().Max(x => x.EventDateTime).ConvertTimeFromUtcToUserTimezone(User);
                var utcNow = DateTime.UtcNow;
                var userTimezoneNow = utcNow.ConvertTimeFromUtcToUserTimezone(User);
                var limitBeforeDt = ConsumeStatusHelper.CalcLimitBeforeDt_InPostPlanning(servTerms.StartDt.ConvertTimeFromUtcToUserTimezone(User), userTimezoneNow, AppConf.Consts.Post_MinuteOfLimitBeforeDt);
                var limitAfterDt = ConsumeStatusHelper.CalcLimitAfterDt_InPostPlanning(servTerms.EndDt.ConvertTimeFromUtcToUserTimezone(User), limitBeforeDt, userTimezoneNow, AppConf.Consts.Post_DaysOfLimitAftereDt);

                dtoScheduler.Settings = new DtoScheduler_SchedulerSettingsViewModel
                {
                    BrandId = brand.BrandId,
                    BrandTitle = "",
                    DefaultActiveDestIds = dtoScheduler.DestItems.Any()?new[]{HxSocialNetworks.Telegram.ToInt()}:new int[] {},// "در اینجا باید به ازای نوع شبکه اجتماعی درگاه‌های مجاز کاربر به استفاده از سیستم زمانبندی آرایه‌ای را بسازیم "
                    DefaultDisableDestIds = socialNetworks.Where(y=>y.SocialNetworkId!=(int)HxSocialNetworks.Telegram).Select(x=>x.SocialNetworkId).ToArray(),// << همه بجز تلگرام   //new int[]{ 1, 2, 3, 4, 5 },
                    FromDt = fromDt,
                    ToDt = maxEventDt,
                    IncludeEventsInLimitSpace = true,
                    InitEvent = closestEventToNow?.ToString("dd/MM/yyyyTHH:mm") ?? "last",
                    // مثلا اگر زمان هست:  10:23
                    // زمان مجاز میشود از 10:23 + 5 >>  10:28 به بعد
                    //LimitBeforeDt = ConsumeStatusHelper.CalcLimitBeforeDt_InPostPlanning(servTerms.EndDt.ConvertTimeFromUtcToUserTimezone(User), userTimezoneNow.AddMinutes(AppConf.Consts.Post_MinuteOfLimitBeforeDt)),
                    LimitBeforeDt = limitBeforeDt,
                    LimitAftereDt = limitAfterDt, 
                    LimitMaxAddContentPack = servTerms.Remain,//AppConf.Consts.Post_LimitMaxAddContentPack, //<< اجازه برنامه ریزی برای حداکثر 20 بسته محتوا
                    LimitMaxCpInEachEvent = LIMIT_MAX_POST_IN_EACH_EVENT_FOR_TELEGRAM_PORT,
                    LoadLocalDt = userTimezoneNow,
                    LoadUtcDt = utcNow
                };

                dtoScheduler.SetNowDtProperties(User);

                //todo آیا آپدیت بالانس پست‌ها در اینجا کار خوبیست؟
                // به نظر میاد که هروقت شرایط سرویس کاربر را به دلیلی میخونیم موقعیت خوبیست که اطلاعات کلایم باتری کاربر را هم با آن آپدیت کنیم
                // لذا
                // شرایط سرویس رو که لود کردیم به کار میگیریم تا اطلاعات باتری تعیین کنندهٔ باتری کاربر رو تغییر بدیم ولی باید حواسمان باشه که
                // در اینجا دقیقا شبیه زیر عمل کنیم. یعنی:
                // فقط در شرایطی که برند جاری متعلق به خود کاربر است ما میتوانیم شرایط خدمتی که لود کرده‌ایم را برای آپدیت کوکی باتری
                // به متد زیر پاس بدهیم در غیر این صورت نباید به متد زیر شرایط سرویس کاربر دیگری را پاس بدهیم!!!
                if (CurUserId == brand.OwnerUserId)
                {
                    User.UpdateUserServs();
                }

                // حذف کلیه لاگها - در این متد کدها را چند بخش کلی تصور کرده‌ایم و پیش از وقوع خطای هر تکه، خطای اون بخش رو در لاگ گذاشته‌ایم
                // تا اگر به خطا خوردیم لاگ مناسبش در پراپرتی لاگها قرار رفته باشه
                // لذا وقتی به پایان کد در اینجا رسیده‌ایم به این معنیه که هیچ خطایی رخ نداده و باید اگر لاگ خطایی در لیست هست، حذفش کنیم
                dtoScheduler.Log.Clear();
            }
            catch (Exception ex)
            {
                // حاوی توضیحات خطا شده باشه و لازم نیست خطای دیگه‌ای اضافه کنیم به لاگ یا رونویسیش کنیم dtoScheduler.Log در اینجا الان باید 
                
                //dtoScheduler.Log.Add(HxException.Generate(ex, errorCode).HxErrorCode);
            }
            return dtoScheduler;
        }

        /// <summary>
        /// یافتن نزدیکترین زمان در لیست ورودی به زمان ورودی
        /// </summary>
        /// <param name="sendersErrorCode"></param>
        /// <param name="targetDt">زمان مدنظر</param>
        /// <param name="dtList">لیست زمانها</param>
        /// <returns></returns>
        [NonAction]
        private static DateTime? ClosestTimeTo(string sendersErrorCode, DateTime targetDt, List<DateTime> dtList)
        {
            var errorCode = CodeOfMethods.WEB_SCHEDULE_CONTROLLER_CLOSEST_TIME_TO.MakeErrorCode(CodeOfProjects.Web, sendersErrorCode);
            DateTime? closestDate = null;
            try
            {
                long min = int.MaxValue;

                foreach (DateTime date in dtList)
                {
                    if (Math.Abs(date.Ticks - targetDt.Ticks) < min)
                    {
                        min = date.Ticks - targetDt.Ticks;
                        closestDate = date;
                    }
                }
            }
            catch (Exception ex)
            {
                throw HxException.Generate(ex, errorCode);
            }
            return closestDate;
        }
        #endregion

        #region Save Content Schedule

        /// <summary>
        /// SAVe Content Schedules >> A
        /// این متد کلیه تغییراتی که کاربر در برنامه داده اععم از موارد اضافه شده، موارد دستکاری شده و موارد حذف شده را بررسی کرده و
        /// در پایگاه داده برای برند درخواستی ذخیره میکند و وضعیت ایجاد شده را طی گزارش از جنس جیسان به کلاینت میفرستد
        /// </summary>
        /// <param name="data">داده ورودی شامل شناسه برند و کلیه اطلاعات اضافه شده، تغییر کرده و حذف شده</param>
        /// <returns>جیسان وضعیت</returns>
        [HttpPost]
        [AjaxOnly]
        public virtual async Task<JsonResult> SaveScheduledContacts(DtoScheduler_SaveDataPackViewModel data)
        {
            var errorCode = CodeOfMethods.WEB_SCHEDULE_CONTROLLER_SAVE_SCHEDULED_CONTACTS.MakeErrorCode(CodeOfProjects.Web);
            // بررسی صحت داده ورودی و شناسه جهانی برند وارد شده
            var dtoScheduler = new DtoScheduler_LoadDataPackViewModel();//ScheduleContentsErrors.FailInSaving_ValidationData);
            try
            {
                if (data != null)
                {
                    var brandGuid = new Guid(data.BrandId);
                    if (brandGuid == new Guid())
                    {
                        dtoScheduler.Log.Add(new ReplyMessageWrapper<ScheduleContentsErrors>(
                            Lang.SchedulingContent_Err_BrandNotFoundDuringSave_Lable,Lang.SchedulingContent_Err_BrandNotFoundDuringSave_Dest,errorCode + "-01",ScheduleContentsErrors.FailInSaving_ValidationData));
                    }
                    else
                    {
                        var brand = _brandService.GetBrand(brandGuid);
                        dtoScheduler = await IfUserHasPermissionToSaveScheduleForSelectedBrand_ThenAction(errorCode,brand, data, SaveScheduledContactsProccess);
                    }
                }
                else
                {
                    dtoScheduler.Log.Add(new ReplyMessageWrapper<ScheduleContentsErrors>(ScheduleContentsErrors.FailInSaving_ValidationData, HxException.Generate(new Exception("Input Data is Null!"), errorCode).HxErrorCode));
                }
            }
            catch (Exception ex)
            {
                dtoScheduler.Log.Add(new ReplyMessageWrapper<ScheduleContentsErrors>(ScheduleContentsErrors.FailInSaving_ValidationData, HxException.Generate(ex, errorCode).HxErrorCode));
            }
            dtoScheduler.SetNowDtProperties(User);
            return Json(dtoScheduler);
        }



        /// <summary>
        /// Save Content Schedules >> B
        /// Check permission to Save
        /// </summary>
        /// <param name="sendersErrorCode">کدخطای متدهای صدا زننده این متد</param>
        /// <param name="brand"></param>
        /// <param name="data"></param>
        /// <param name="saveActionAsync"></param>
        /// <returns></returns>
        [NonAction]
        private async Task<DtoScheduler_LoadDataPackViewModel> IfUserHasPermissionToSaveScheduleForSelectedBrand_ThenAction(
            string sendersErrorCode, Brand brand, DtoScheduler_SaveDataPackViewModel data, Func<string, Brand, DtoScheduler_SaveDataPackViewModel, ServTerms, Task<Tuple<bool, List<ReplyMessageWrapper<ScheduleContentsErrors>>>>> saveActionAsync)
        {
            var errorCode = CodeOfMethods.WEB_SCHEDULE_CONTROLLER_IF_HAS_PERMISSION_TO_SAVE_SCHEDULED_CONTACTS_THEN_ACTION.MakeErrorCode(CodeOfProjects.Web, sendersErrorCode);
            var dtoScheduler = new DtoScheduler_LoadDataPackViewModel();
            try
            {
                var servTerms = _consManager.GetBalanceOf_PostPlanningServ(brand.OwnerUserId.Value);
                if (servTerms != null)
                {
                    //_______________________________________________________________________________________________________________________________
                    // اطلاعات برندهایی که کاربر مجاز به مدیریت آنهاست را واکشی میکنیم
                    var accessedBrands = _brandService.GetAllBrands_ThatThisUserHasAcceessToThem(CurUserId)
                        .Select(x => new BrandItem_ToSchedulingContentViewModel
                        {
                            Title = x.Title,
                            BrandGuid = x.BrandGuid, //<< برای امنیت از جی‌یو‌آی‌دی بجای شناسه برند در ویو استفاده میکنم
                            BrandId = x.BrandId,
                            BrandOwnerUserId = x.OwnerUserId.Value,
                            ConsumeServiceStatus = ConsumeServiceStatus.AllowdService  //<< خودمان همه برندها را مجاز تعریف میکنیم چون مجاز بودن درباره پورت‌ها باید چک شود نه برند و ما آن بررسی را در فرایند ولیدیشن داد‌ها که بعد از این متد است انجام میدهیم
                        })
                        .ToList();

                    // اگر برند مجاز نبود یا یافت نشد
                    if (accessedBrands == null || !accessedBrands.Any())
                    {
                        dtoScheduler.Log.Add(new ReplyMessageWrapper<ScheduleContentsErrors>(
                            Lang.SchedulingContent_Err_AccessDenyToBrandDuringSave_Lable, Lang.SchedulingContent_Err_AccessDenyToBrandDuringSave_Dest, errorCode + "-01", ScheduleContentsErrors.FailInSaving_ValidationData));
                    }
                    else
                    {
                        // "لازم نیست چک کنیم ببینیم که این برند مجاز است یا نه. چرا که برندها مجازند و باید مجاز بودن پورت چک شود که در مرحله ولیدیشن بررسی میشوند"

                        //var accessedBrand = accessedBrands.FirstOrDefault(x => x.BrandId == brand.BrandId);
                        //if (accessedBrand == null)
                        //{
                        //    replyData.Error = ScheduleContentsErrors.FailInSaving_ValidationData;
                        //    replyData.SetReplyMessage(Lang.SchedulingContent_Err_AccessDenyToBrandDuringSave_Lable, Lang.SchedulingContent_Err_AccessDenyToBrandDuringSave_Dest, errorCode + "-02");
                        //}
                        //else
                        //{
                        //    // ______________ چک میکنیم ببینیم آیا این برند مجاز به مصرف سرویس زمانبندی محتوا هست؟ _________________________________________________________

                        //    var consumeServiceStatus_Of_AccessedBrands = _portXrefServTypeService.IsBrandAllowedConsumeTheService(brand.BrandId, HxServTypes.Planning);
                        //    if (consumeServiceStatus_Of_AccessedBrands == ConsumeServiceStatus.AllowdService)
                        //    {
                        //        replyData = await saveActionAsync(errorCode, brand, data, servTerms);

                        //    }
                        //    else
                        //    {
                        //        replyData.Error = ScheduleContentsErrors.FailInSaving_ValidationData;
                        //        replyData.SetReplyMessage(consumeServiceStatus_Of_AccessedBrands.GetEnumDisplayName(), consumeServiceStatus_Of_AccessedBrands.GetEnumDisplayDescription(), errorCode + "-03");
                        //    }
                        //}
                        
                        // برو برای ذخیره برنامه برند مربوطه
                        var saveResult = await saveActionAsync(errorCode, brand, data, servTerms);
                        
                        // آیتم 2 حاوی اطلاعات لاگ مسیرهای طی شده در فرایند ذخیره سازیه
                        dtoScheduler.Log = saveResult.Item2;

                        // آیتم1 مقدار بولینی دارد که وضعیت ولیدیشن موفق یا ناموفق را حمل میکنه
                        // اگر ولیدیشن موفق بوده پس ممکنه داده‌ها هم ذخیره شده باشه پس مقدار رلود داده‌ها در این حالت ترو میشه
                        // در غیر این صورت ریلود داده‌ها فالس میمونه و فرمانی برای ریلود شدن داده‌ها در سمت ینت از سوی ابزار برنامه‌ریز تولید نخواهد شد
                        dtoScheduler.ReloadData = saveResult.Item1;
                    }
                }
                else
                {
                    dtoScheduler.Log.Add(new ReplyMessageWrapper<ScheduleContentsErrors>(
                        Lang.SchedulingContent_Err_ServTermsIsNullDuringSave_Lable, Lang.SchedulingContent_Err_ServTermsIsNullDuringSave_Dest, errorCode + "-04", ScheduleContentsErrors.FailInSaving_ValidationData));
                    dtoScheduler.ReloadData = false;
                }
            }
            catch (Exception ex)
            {
                dtoScheduler.Log.Add(new ReplyMessageWrapper<ScheduleContentsErrors>(ScheduleContentsErrors.FailInSaving_ValidationData,errorCode));
                dtoScheduler.ReloadData = false;
            }
            return dtoScheduler;
        }

        /// <summary>
        /// SAVe Content Schedules >> C
        /// Save proccess
        /// </summary>
        /// <param name="sendersErrorCode">کدخطاهای متدهای صدا زننده این متد</param>
        /// <param name="brand"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        [NonAction]
        private async Task<Tuple<bool, List<ReplyMessageWrapper<ScheduleContentsErrors>>>> SaveScheduledContactsProccess(string sendersErrorCode, Brand brand, DtoScheduler_SaveDataPackViewModel data, ServTerms servTerms)
        {
            var errorCode = CodeOfMethods.WEB_SCHEDULE_CONTROLLER_SAVE_SCHEDULED_CONTACTS_PROCCESS.MakeErrorCode(CodeOfProjects.Web, sendersErrorCode);
            //var replyData = new DtoScheduler_LoadDataPackViewModel(ScheduleContentsErrors.FailInSaving_ValidationData);
            var logs = new List<ReplyMessageWrapper<ScheduleContentsErrors>>();
            bool dataSaved=false;
            var utcNow = DateTime.UtcNow;
            try
            {
                // -----------واکشی برخی مایحتاج فرایند از پایگاه------------------------------------------------
                // واکشی برند مربوطه و واکشی یکجای تمام درگاه‌های مرتبط با این برند
                var portAdmins_OfCurrentUser_ForTargetBrand = _portAdminService.GetPortAdmins_ByUseIdAndBrandIdAsync(CurUserId, brand.BrandId, false);

                var ports_OfCurrentUser_ForTargetBrand = new List<Port>();
                // پر کردن لیست پرت‌های مدیریتی کاربر جاری که متعلق به برند درخواستی هستند از روی رکوردهای مدیریتی  این کاربر برای آن برند 
                ports_OfCurrentUser_ForTargetBrand.AddRange(portAdmins_OfCurrentUser_ForTargetBrand.Select(portAdmin => portAdmin.Port));

                //------------------ ورود به فرایند اعتبارسنجی‌داده‌ها و حذف خودکار آیتم‌های نا معتبر --------------------------------------------
                var validationResult = Save_ValidateData(sendersErrorCode,brand.OwnerUserId.Value, CurUserId, brand.BrandId, data, servTerms, portAdmins_OfCurrentUser_ForTargetBrand.Select(x=>new {x.Port.PortId,x.Port.SocialNetworkId}).ToDictionary(k => k.PortId, i => i.SocialNetworkId));

                // آیا ولیدیشن موفق بود؟
                if (validationResult.Item1)
                {

                    // میریم برای ذخیره داده‌ها
                    dataSaved = true;

                    var increaseRemain = false;
                    var decreaseRemain = false;
                    try
                    {
                        if (data.Removed != null && data.Removed.Any())
                        {
                            try
                            {
                                var removedPosts = SaveRemovedSchedules(sendersErrorCode, CurUserId, data, utcNow);
                                if (removedPosts.Detail.Count > 0)
                                {
                                    // افزایش دادن باقیمانده مجاز برنامه‌ریزی‌ها
                                    increaseRemain = await _consManager.ConsumePostPlanningServ(removedPosts.Result.Select(x => x.PostId).ToList(), ConsumeTypes.Refund, brand.OwnerUserId.Value);
                                    if (increaseRemain == false)
                                    {
                                        //todo فرایند افزایش دادن باقیمنده مجاز مصرف کاربر با مشکل یا خطایی روبرو شده بوده پس باید یک علامت گذاری در کوکی یا کاری بکینیم که در اولین فرصت مقدار آن تصحیح شود
                                        throw new Exception("خطایی در پی حذف چند آیتم از برنامه پست‌ها و افزایش دادن تعداد باقیمانده پستهای کاربر بوجود آمد");
                                    }
                                }
                                logs.Add(new ReplyMessageWrapper<ScheduleContentsErrors>(Lang.SchedulingContent_DeletedItemsSaved_Lable, string.Format(Lang.SchedulingContent_DeletedItemsSaved_Desc, removedPosts.Result.Count)));
                            }
                            catch (Exception e)
                            {
                                logs.Add(new ReplyMessageWrapper<ScheduleContentsErrors>(ScheduleContentsErrors.FailInSaving_Removed));
                            }
                        }

                        
                        if (data.Added != null && data.Added.Any())
                        {
                            // اگر تعداد پست ماندهٔ سرویس زمانبندی به قدر تعداد پست اضافه‌شده در برنامه هست 
                            if (servTerms.Remain >= data.Added.Count())
                            {
                                try
                                {
                                    var addedPosts = await SaveAddedSchedulesAsync(sendersErrorCode, CurUserId, data, brand, portAdmins_OfCurrentUser_ForTargetBrand, ports_OfCurrentUser_ForTargetBrand, utcNow);

                                    // کاهش دادن باقیمانده مجاز برنامه‌ریزی‌ها
                                    if (addedPosts.Detail.Count > 0)
                                    {
                                        decreaseRemain = await _consManager.ConsumePostPlanningServ(addedPosts.Result.Select(x => x.PostId).ToList(), ConsumeTypes.Use, brand.OwnerUserId.Value);
                                        if (decreaseRemain == false)
                                        {
                                            //todo فرایند کاهش دادن باقیمنده مجاز مصرف کاربر با مشکل یا خطایی روبرو شده بوده پس باید یک علامت گذاری در کوکی یا کاری بکینیم که در اولین فرست مقدار آن تصحیح شود
                                            throw new Exception("خطایی در پی افزودن چند آیتم به برنامه پست‌ها و کاهش دادن تعداد باقیمانده پستهای کاربر بوجود آمد");
                                        }
                                    }
                                    logs.Add(new ReplyMessageWrapper<ScheduleContentsErrors>(Lang.SchedulingContent_AddedItemsSaved_Lable,string.Format(Lang.SchedulingContent_AddedItemsSaved_Desc, addedPosts.Result.Count)));
                                }
                                catch (Exception e)
                                {
                                    logs.Add(new ReplyMessageWrapper<ScheduleContentsErrors>(ScheduleContentsErrors.FailInSaving_Added));
                                }
                            }
                            else
                            {
                                // الان جایی هستیم که کاربر متاسفانه باقیمانده پست کافی برای افزودن نداشته در حسابش

                                logs.Add(new ReplyMessageWrapper<ScheduleContentsErrors>(
                                    Lang.SchedulingContent_Err_RemainPostIsNotEnoughToSave_Lable,
                                    brand.OwnerUserId == CurUserId
                                        ? Lang.SchedulingContent_Err_RemainPostIsNotEnoughToSaveForBrandOwner_Dest
                                        : Lang.SchedulingContent_Err_RemainPostIsNotEnoughToSaveForBrandAdmin_Dest,
                                    errorCode + "-01",
                                    ScheduleContentsErrors.FailInSaving_Added));

                                //replyData.Error = ScheduleContentsErrors.FailInSaving_Added;
                                //replyData.SetReplyMessage(
                                //    Lang.SchedulingContent_Err_RemainPostIsNotEnoughToSave_Lable,
                                //    brand.OwnerUserId == CurUserId
                                //        ? Lang.SchedulingContent_Err_RemainPostIsNotEnoughToSaveForBrandOwner_Dest
                                //        : Lang.SchedulingContent_Err_RemainPostIsNotEnoughToSaveForBrandAdmin_Dest,
                                //    errorCode + "-01");
                            }
                        }

                        

                        if (data.Modified != null && data.Modified.Any())
                        {
                            try
                            {
                                SaveModifiedSchedules(sendersErrorCode, CurUserId, data, brand, portAdmins_OfCurrentUser_ForTargetBrand, ports_OfCurrentUser_ForTargetBrand, utcNow);
                                logs.Add(new ReplyMessageWrapper<ScheduleContentsErrors>(Lang.SchedulingContent_ModifiedItemsSaved_Lable, string.Format(Lang.SchedulingContent_ModifiedItemsSaved_Desc, data.Modified.Length)));
                            }
                            catch (Exception e)
                            {
                                logs.Add(new ReplyMessageWrapper<ScheduleContentsErrors>(ScheduleContentsErrors.FailInSaving_Modified));
                            }
                            
                        }
                    }
                    finally
                    {
                        //todo آیا آپدیت بالانس پست‌ها در اینجا کار خوبیست؟
                        if (decreaseRemain || increaseRemain)
                        {
                            //آپدیت کردن تعداد پست مجاز جهت برنامه ریزی در آیدنتیتی کاربر > کوکی
                            User.UpdateUserServs();
                        }
                    }
                }
                // اگر ولیدیشن موفق نبوده
                else
                {
                    // پس داده‌ای هم ذخیره نشده
                    dataSaved = true;

                    var replyMessage = validationResult.Item2;
                    logs.Add(new ReplyMessageWrapper<ScheduleContentsErrors>(replyMessage.Title, replyMessage.Description, replyMessage.Code, ScheduleContentsErrors.FailInSaving_ValidationData));
                }
            }
            catch (Exception ex)
            {
                logs.Add(new ReplyMessageWrapper<ScheduleContentsErrors>(ScheduleContentsErrors.FailInSaving_ValidationData,errorCode));
            }
            
            return new Tuple<bool, List<ReplyMessageWrapper<ScheduleContentsErrors>>>(dataSaved,logs);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sendersErrorCode"></param>
        /// <param name="ownerUserId"></param>
        /// <param name="brandOwnerUserId"></param>
        /// <param name="brandId"></param>
        /// <param name="data"></param>
        /// <param name="servTerms"></param>
        /// <param name="listOf_Managable_PortIdAndItsSocialNetworkId_ForCurrentUser_ForTargetBrand">
        /// لیست پورتها و شبکه‌اجتماعی‌های آنها
        /// که کاربر در ازای برند جاری مجاز به مدیریت آنهاست
        /// و سوئیچ‌های اشاره کننده به آنها در ابزار زمانبندی‌اش میتواند قرار گرفته باشد
        /// </param>
        /// <returns></returns>
        [NonAction]
        private Tuple<bool, ReplyMessageWrapper<ScheduleContentsErrors>> Save_ValidateData(string sendersErrorCode, long ownerUserId, long brandOwnerUserId, int brandId, DtoScheduler_SaveDataPackViewModel data, ServTerms servTerms, Dictionary<int,int> listOf_Managable_PortIdAndItsSocialNetworkId_ForCurrentUser_ForTargetBrand)
        {
            var errorCode = CodeOfMethods.WEB_SCHEDULE_CONTROLLER_VALIDATE_SCHEDULES_DATA_TO_SAVE.MakeErrorCode(CodeOfProjects.Web, sendersErrorCode);
            var utcNow = DateTime.UtcNow;
            var userTimezoneNow = utcNow.ConvertTimeFromUtcToUserTimezone(User);
            var weHasAdded = data.Added != null && data.Added.Any();
            var weHasModified = data.Modified != null && data.Modified.Any();
            var weHasRemoved = data.Removed != null && data.Removed.Any();

            try
            {
                //################################################################################################################################
                // در سیستم ما هر برند صرفا یک پورت میتواند داشته باشد در هر شبکه اجتماعی
                
                // اول میرویم ببینیم آیا کاربر برای شبکه‌های اجتماعی از برند مربوطه که در آنها مجاز بوده‌ از سرویس زمانبندی بهرهمند شود برنامه‌ریز کرده یا خیر
                var allowedSocialNetwork = _consManager
                    .GetSocialNetworks_OfPortsOfBrand_ThaHave_ActiveCurrentPortAssing_ToTheServTyp_ToConsumption(brandId, HxServTypes.Posting)
                    .Select(x=>x.SocialNetworkId);

                if (weHasAdded)
                {
                    IEnumerable<int> scheduledPorts = new List<int>();
                    //شناسه تمام پورتهایی که در لیست ادد برای آنها زمانبندی شده را بدست می‌آوریم
                    scheduledPorts = data.Added.Aggregate(scheduledPorts, (current, added) => current.Union(added.Schedules.Select(x => x.DestId)));
                    if (scheduledPorts.Any(socialNetworkId => !allowedSocialNetwork.Contains(socialNetworkId)))
                    {
                        return new Tuple<bool, ReplyMessageWrapper<ScheduleContentsErrors>>(false, new ReplyMessageWrapper<ScheduleContentsErrors>(
                            Lang.SchedulingContent_Err_AddedScheduleForNotAssignedPort_Lable, Lang.SchedulingContent_Err_AddedScheduleForNotAssignedPort_Dest));
                    }
                }
                if (weHasModified)
                {
                    IEnumerable<int> scheduledPorts = new List<int>();
                    //شناسه تمام پورتهایی که در لیست ادد برای آنها زمانبندی شده را بدست می‌آوریم
                    scheduledPorts = data.Modified.Aggregate(scheduledPorts, (current, modified) => current.Union(modified.Schedules.Select(x => x.DestId)));
                    if (scheduledPorts.Any(socialNetworkId => !allowedSocialNetwork.Contains(socialNetworkId)))
                    {
                        return new Tuple<bool, ReplyMessageWrapper<ScheduleContentsErrors>>(false, new ReplyMessageWrapper<ScheduleContentsErrors>(
                            Lang.SchedulingContent_Err_ModifiedScheduleForNotAssignedPort_Lable, Lang.SchedulingContent_Err_ModifiedScheduleForNotAssignedPort_Dest));
                    }
                }
                if (weHasRemoved)
                {
                    IEnumerable<int> scheduledPorts = new List<int>();
                    //شناسه تمام پورتهایی که در لیست ادد برای آنها زمانبندی شده را بدست می‌آوریم
                    scheduledPorts = data.Removed.Aggregate(scheduledPorts, (current, removed) => current.Union(removed.Schedules.Select(x => x.DestId)));
                    if (scheduledPorts.Any(socialNetworkId => !allowedSocialNetwork.Contains(socialNetworkId)))
                    {
                        return new Tuple<bool, ReplyMessageWrapper<ScheduleContentsErrors>>(false, new ReplyMessageWrapper<ScheduleContentsErrors>(
                            Lang.SchedulingContent_Err_RemovedScheduleForNotAssignedPort_Lable, Lang.SchedulingContent_Err_RemovedScheduleForNotAssignedPort_Dest));
                    }
                }

                //################################################################################################################################
                // ______________ افزون بر چکهای کلاینت سایدی اینجا هم برای اطمینان نسبت به حذف زمانبندی های خارج از محدوده اقدام میکنیم _________________________________________________________
                //var limitAftereDt = ConsumeStatusHelper.CalcLimitAfterDt_InInPostPlanning(servTerms.StartDt.ConvertTimeFromUtcToUserTimezone(User), userTimezoneNow + TimeSpan.FromDays(AppConf.Consts.Post_DaysOfLimitAftereDt));
                //var limitBeforeDt = ConsumeStatusHelper.CalcLimitBeforeDt_InInPostPlanning(servTerms.EndDt.ConvertTimeFromUtcToUserTimezone(User), userTimezoneNow.AddMinutes(AppConf.Consts.Post_MinuteOfLimitBeforeDt));
                var limitBeforeDt = ConsumeStatusHelper.CalcLimitBeforeDt_InPostPlanning(servTerms.StartDt.ConvertTimeFromUtcToUserTimezone(User), userTimezoneNow, AppConf.Consts.Post_MinuteOfLimitBeforeDt);
                var limitAfterDt = ConsumeStatusHelper.CalcLimitAfterDt_InPostPlanning(servTerms.EndDt.ConvertTimeFromUtcToUserTimezone(User), limitBeforeDt, userTimezoneNow, AppConf.Consts.Post_DaysOfLimitAftereDt);
                
                //__________________________________________________
                if (weHasAdded)
                {
                    var addedList = new List<DtoScheduler_AddedItemViewModel>(data.Added);
                    foreach (var added in data.Added.Where(added => limitBeforeDt > added.EventDt || limitAfterDt < added.EventDt))
                    {
                        addedList.Remove(added);
                    }
                    data.Added = addedList.ToArray();
                }
                //__________________________________________________
                if (weHasModified)
                {
                    var modifiedList = new List<DtoScheduler_ModifiedItemViewModel>(data.Modified);
                    foreach (var modified in data.Modified.Where(modified => limitBeforeDt > modified.EventDt || limitAfterDt < modified.EventDt))
                    {
                        modifiedList.Remove(modified);
                    }
                    data.Modified = modifiedList.ToArray();
                }
                //__________________________________________________
                if (weHasRemoved)
                {
                    var removedList = new List<DtoScheduler_RemovedItemViewModel>(data.Removed);
                    foreach (var removed in data.Removed.Where(removed => limitBeforeDt > removed.EventDt || limitAfterDt < removed.EventDt))
                    {
                        removedList.Remove(removed);
                    }
                    data.Removed = removedList.ToArray();
                }
                //################################################################################################################################
                // ______________ زمانهای رویدادهای را یکبار برای اطمینان به شکل لازم روند میکنیم _____________________________________________________________________________________________________

                var ceilInterval = TimeSpan.FromMinutes(5); //<< روند کن به بالا طوری که عدد دقیقه رویدادها مضرب 5 شود
                if (weHasAdded)
                {
                    foreach (var item in data.Added)
                    {
                        item.EventDt.Ceil(ceilInterval);
                    }
                }
                if (weHasModified)
                {
                    foreach (var item in data.Modified)
                    {
                        item.EventDt.Ceil(ceilInterval);
                    }
                }
                if (weHasRemoved)
                {
                    foreach (var item in data.Removed)
                    {
                        item.EventDt.Ceil(ceilInterval);
                    }
                }

                if ((weHasAdded || weHasModified || weHasRemoved) && ((data.Added?.Count() ?? 0) + (data.Modified?.Count() ?? 0) + (data.Removed?.Count() ?? 0) <= 0))
                {
                    return new Tuple<bool, ReplyMessageWrapper<ScheduleContentsErrors>>(false, new ReplyMessageWrapper<ScheduleContentsErrors>(
                        Lang.SchedulingContent_Err_NothingExistsToSaveAfterValidation_Lable, Lang.SchedulingContent_Err_NothingExistsToSaveAfterValidation_Dest));
                }

                //################################################################################################################################
                // چک میکنیم ببینیم که کاربر در هر رویداد بیش از حد مطلب 
                // برای انتشار در شبکه تلگرام جا نداده باشد 
                //چون تعداد محتوا در هر رویداد محدودیت دارد
                //و خلاف این باعث خطای 429 ربات ما میشود
                const int TELEGRAM_SOCIALNET_ID = (int)HxSocialNetworks.Telegram;
                if (!listOf_Managable_PortIdAndItsSocialNetworkId_ForCurrentUser_ForTargetBrand.ContainsValue(TELEGRAM_SOCIALNET_ID))
                {
                    //throw HxException.Generate(new Exception("The port not found!"), $"{errorCode}_1");
                    return new Tuple<bool, ReplyMessageWrapper<ScheduleContentsErrors>>(false, new ReplyMessageWrapper<ScheduleContentsErrors>(
                        "Port Error",
                        $"The port not found!",
                        $"{errorCode}_1"));
                }
                var portOfTelegram = listOf_Managable_PortIdAndItsSocialNetworkId_ForCurrentUser_ForTargetBrand.FirstOrDefault(x => x.Value == TELEGRAM_SOCIALNET_ID).Value;
                // تعداد مطلب برنامه‌ریزی شده جدید در هر رویداد که البته برنامهریزی مربوطه راجع به پورت تلگرامی باشد 
                var event_AddedCount =
                    data?.Added?
                        // جاهایی برای پورت تلگرامی محتوای جدیدی برنامه‌ریزی شده 
                        .Where(p => p.Schedules.Any(z => z.DestId == TELEGRAM_SOCIALNET_ID))
                        .GroupBy(g => g.EventDt)
                        .Select(g => new DtIntPair {Dt = g.Key, Count = g.Count()})
                        .ToDictionary(k => k.Dt, i => i.Count)
                    ?? new Dictionary<DateTime, int>();

                // تعداد مطلب حذف شده از برنامه در هر رویداد که البته آن حذف راجع به پورت تلگرامی باشد 
                var event_RemovedCount =
                    data?.Removed?
                        // جاهایی برای پورت تلگرامی محتوای جدیدی برنامه‌ریزی شده 
                        .Where(p => p.Schedules.Any(z => z.DestId == TELEGRAM_SOCIALNET_ID))
                        .GroupBy(f => f.EventDt)
                        .Select(g => new DtIntPair {Dt = g.Key, Count = g.Count()})
                        .ToDictionary(k => k.Dt, i => i.Count)
                    ?? new Dictionary<DateTime, int>();

                //تعداد آیتم اضافه شده در هر رویداد بعد از کسر تعداد آیتم حذف شده
                var newItems = new Dictionary<DateTime, int>();
                // کم کردن حذف شده‌های تپورت تلگرامی از اضافه شده‌های پورت تلگرامی در هر رویداد
                foreach (var event_Count in event_AddedCount)
                {
                    var deletedCount = 0;
                    // اگر در این تاریخ پست حذف شده‌ای داشتیم
                    if (event_RemovedCount.ContainsKey(event_Count.Key))
                    {
                        // تعداد حذف شده‌ها
                         deletedCount = event_RemovedCount.FirstOrDefault(x => x.Key == event_Count.Key).Value ;
                    }
                    // اگر تعداد اضافه شده‌ها از حذف شده‌ها بیشتر بود
                    if (event_Count.Value > deletedCount)
                    {
                        // افزودن تعداد آیتم اضافه شده در این رویداد بعد از کسر تعداد آیتم حذف شده
                        newItems.Add(event_Count.Key,event_Count.Value-deletedCount);
                    }
                }
                //چک کردن اینکه آیا تعداد آیتم اضافه شده از حدنصاب پست قابل برنامه‌ریزی در هر رویداد گذشته است یا خیر
                var status = _postService.CheckLimitOf_MaxPostInEchEvent(newItems, portOfTelegram, LIMIT_MAX_POST_IN_EACH_EVENT_FOR_TELEGRAM_PORT);
                if (!status.Result)
                {
                    //throw HxException.Generate(new Exception("), $"{errorCode}_2");
                    return new Tuple<bool, ReplyMessageWrapper<ScheduleContentsErrors>>(false, new ReplyMessageWrapper<ScheduleContentsErrors>(
                        "Telegram Error",
                        $"You put more than {LIMIT_MAX_POST_IN_EACH_EVENT_FOR_TELEGRAM_PORT} Telegram posts ({status.Detail.Count} posts) in {status.Detail.Dt.ConvertTimeFromUtcToUserTimezone(User)} event of your plan!<br>Count of maximum allowed post in each event is {LIMIT_MAX_POST_IN_EACH_EVENT_FOR_TELEGRAM_PORT}.",
                        $"{errorCode}_2"));
                }

                //################################################################################################################################
                // برابر داشته باشیم این باعث ایجاد اختلالی بدی میشه که در کارت زیر در ترلو توضیح داده‌ام Priority نباید در یک رویداد دو آیتم با 
                // https://trello.com/c/ij0C02iX
                // به همین دلیل داده‌های ورودی را چک میکنیم که چنین اشکالی درش نباشه
                //foreach (var VARIABLE in data.Added.Select(x=>x.))
                //{
                    
                //}
                //foreach (var VARIABLE in )
                //{

                //}
                //data.Added.Select(x => x.EventDt)
                //LIMIT_MAX_CP_IN_EACH_EVENT
            }
            //catch (HxException)
            //{
            //    throw;
            //}
            catch (Exception ex)
            {
                // throw HxException.Generate(ex, errorCode);
                //logs.Add(new ReplyMessageWrapper<ScheduleContentsErrors>());
                return new Tuple<bool, ReplyMessageWrapper<ScheduleContentsErrors>>(false, new ReplyMessageWrapper<ScheduleContentsErrors>(ScheduleContentsErrors.FailInSaving_ValidationData, errorCode));
            }
            return new Tuple<bool, ReplyMessageWrapper<ScheduleContentsErrors>>(true, new ReplyMessageWrapper<ScheduleContentsErrors>(null, null));
        }

        [NonAction]
        private async Task<ResultWithDetail<List<Post>, Dictionary<int, int>>> SaveAddedSchedulesAsync(string sendersErrorCode, long curUserId, DtoScheduler_SaveDataPackViewModel data, Brand brand,
            List<PortAdmin> portAdmins_OfCurrentUser_ForTargetBrand, List<Port> ports_OfCurrentUser_ForTargetBrand, DateTime utcNow)
        {
            var errorCode = CodeOfMethods.WEB_SCHEDULE_CONTROLLER_VALIDATE_SCHEDULES_DATA_TO_SAVE.MakeErrorCode(CodeOfProjects.Web, sendersErrorCode);
            try
            {
                var portCount = new Dictionary<int, int>();
                List<Post> newPosts = new List<Post>();

                if (data.Added == null)
                {
                    return new ResultWithDetail<List<Post>, Dictionary<int, int>>(newPosts,portCount);
                }
                // -----------واکشی از پایگاه------------------------------------------------
                // واکشی یکجای تمام بسته‌محتواهای موجود در لیست اضافه‌شده‌ها و زمانبندی‌های احتمالی از قبل ایجاد شده برای آنها 
                var cpList = _contentPackService.GetContentPacks(data.Added.Select(x => x.CpId - ContentPack.CONTENTPACK_BASE_CODE).ToArray(), includePosts: true);
                
                foreach (var item in data.Added)
                {
                    var publishDtAsUtc = item.EventDt.ConvertTimeToUtcFromUserTimezone(User);
                    var cpId = item.CpId - ContentPack.CONTENTPACK_BASE_CODE;
                    var cp = cpList.FirstOrDefault(x => x.ContentPackId == cpId);
                    var priority = item.CpIndex;
                    foreach (var requestedSch in item.Schedules)
                    {
                        var socialNetworkId = requestedSch.DestId.ConverToEnum<HxSocialNetworks>();

                        // حالا که شبکه شبکه اجتماعی هدف را یافته‌ایم چک میکنیم ببینیم آیا کاربر قبلا محتوای هماهنگ با آن را ساخته یا
                        // باید خودمان به طور خودکار یکی درخور آن بسازیم

                        var existSchsInDb_ButInactive = cp.Posts.FirstOrDefault(x =>
                                        x.PostId!=0 //<< در پایگاه ذخیره شده باشه نه در کش انتیتی فریمورک
                                    &&  x.PublishDt == publishDtAsUtc 
                                    &&  x.SocialNetworkId == (int) socialNetworkId
                                    &&  x.BrandId==brand.BrandId
                                    &&  x.IsDeleted
                                    );

                        if (existSchsInDb_ButInactive == null)
                        {
                            var port = ports_OfCurrentUser_ForTargetBrand.FirstOrDefault(x => x.SocialNetworkId == (int) socialNetworkId);
                            // اگر پورت مربوط به این شبکه اجتماعی رو هنوز نساخته
                            if (port == null)
                                continue; //<< برو سراغ آیتم بعدی
                            var portAdmin = portAdmins_OfCurrentUser_ForTargetBrand.FirstOrDefault(x => x.PortId == port.PortId);
                            // اگر رکورد مدیریت پورت این شبکه اجتماعی برای کاربر جاری هنوز جفت و جور نشده
                            if (portAdmin == null)
                                continue; //<< برو سراغ آیتم بعدی
                            var addedPost=await AddContentForTargetSocialNetworkAsync(errorCode, brand, curUserId, cp, port, portAdmin, priority, publishDtAsUtc, socialNetworkId, utcNow);
                            newPosts.Add(addedPost);
                            // پرکردن دیکشنری نتیجه
                            if (portCount.ContainsKey(addedPost.PortId))
                            {
                                portCount[addedPost.PortId] = portCount[addedPost.PortId] + 1;
                            }
                            else
                            {
                                portCount.Add(addedPost.PortId, 1);
                            }
                        }
                        else
                        {
                            //------------ Modify the exists content schedule--------------------------------------
                            if (_postService.ActivatePost(existSchsInDb_ButInactive, saveChanges: false, priority: priority))
                            {
                                newPosts.Add(existSchsInDb_ButInactive);
                                // پرکردن دیکشنری نتیجه
                                if (portCount.ContainsKey(existSchsInDb_ButInactive.PortId))
                                {
                                    portCount[existSchsInDb_ButInactive.PortId] = portCount[existSchsInDb_ButInactive.PortId] + 1;
                                }
                                else
                                {
                                    portCount.Add(existSchsInDb_ButInactive.PortId, 1);
                                }
                            }
                        }
                    }
                }
                _uow.SaveChanges();
                return new ResultWithDetail<List<Post>,Dictionary<int,int>>(newPosts,portCount); // data.Added.Count();
            }
            catch (Exception ex)
            {
                throw HxException.Generate(ex, errorCode);
            }
        }

        [NonAction]
        private void SaveModifiedSchedules(string sendersErrorCode, long curUserId, DtoScheduler_SaveDataPackViewModel data, Brand brand,
            IEnumerable<PortAdmin> portAdmins_OfCurrentUser_ForTargetBrand, IEnumerable<Port> ports_OfCurrentUser_ForTargetBrand, DateTime utcNow)
        {
            var errorCode = CodeOfMethods.WEB_SCHEDULE_CONTROLLER_SAVE_MODIFIED_SCHEDULES.MakeErrorCode(CodeOfProjects.Web, sendersErrorCode);
            if (data.Modified == null)
                return;
            try
            {
                // واکشی تمام زمانبندی‌های موجود در لیست تغییرکرده‌ها

                //ــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــ
                // اعمال تغییرات مربوط به فرمان انتشار یک محتوا در یک شبکه‌اجتماعی دیگر
                // این تغییرات مربوط به روشن شدن سوئیچ انتشار یک محتوای موجود در برنامه جهت منتشر شدن در یک شبکه اجتماعی است
                var activatedItems = data.Modified.Where(x => x.ModifyTypes.Contains(SchedullerModifyTypes.ScheduleActivated)).ToList();
                var cpList_Of_ActivatedItems = _contentPackService.GetContentPacks(activatedItems.Select(x => HxUtility.CpCodeToCpId(x.CpId)).ToArray(), includePosts: true);


                VerifyIfSwitchChanged(errorCode, VerifySwitchTypes.IfActivated, activatedItems, async (changedItem, schItem) =>
                {
                    var priority = changedItem.CpIndex;
                    var cpId = HxUtility.CpCodeToCpId(changedItem.CpId);
                    var cp = cpList_Of_ActivatedItems.FirstOrDefault(x => x.ContentPackId == cpId);
                    if (cp == null)
                        return;
                    var existSchs_For_ThisCp_ThisSocialNetwork_ThidPublishDt = cp.Posts.FirstOrDefault(x => x.PublishDt == changedItem.EventDt &&
                                                                                                                       x.SocialNetworkId == schItem.DestId);
                    //_postService.ActiveSchedule(existSchs_For_ThisCp_ThisSocialNetwork_ThidPublishDt, saveChanges: false);
                    if (existSchs_For_ThisCp_ThisSocialNetwork_ThidPublishDt == null)
                    {

                        var socialNetworkId = schItem.DestId.ConverToEnum<HxSocialNetworks>();
                        var port = ports_OfCurrentUser_ForTargetBrand.FirstOrDefault(x => x.SocialNetworkId == (int) socialNetworkId);
                        // اگر پورت مربوط به این شبکه اجتماعی رو هنوز نساخته
                        if (port == null)
                            return; //<< برو سراغ آیتم بعدی
                        var portAdmin = portAdmins_OfCurrentUser_ForTargetBrand.FirstOrDefault(x => x.PortId == port.PortId);
                        // اگر رکورد مدیریت پورت این شبکه اجتماعی برای کاربر جاری هنوز جفت و جور نشده
                        if (portAdmin == null)
                            return; //<< برو سراغ آیتم بعدی
                        var publishDtAsUtc = changedItem.EventDt.ConvertTimeToUtcFromUserTimezone(User);
                        await AddContentForTargetSocialNetworkAsync(errorCode, brand, curUserId, cp, port, portAdmin, priority, publishDtAsUtc, socialNetworkId, utcNow);
                    }
                    else
                    {
                        //------------ Modify the exists content schedule--------------------------------------
                        _postService.ActivatePost(existSchs_For_ThisCp_ThisSocialNetwork_ThidPublishDt, saveChanges: false, priority: priority);
                    }
                });

                //ــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــ
                // اعمال تغییرات مربوط به فرمان لغو انتشار یک محتوا در یک شبکه‌اجتماعی 
                // این تغییرات مربوط به خاموش شدن سوئیچ انتشار یک محتوای موجود در برنامه جهت منتشر نشدن در یک شبکه اجتماعی است
                var deActivatedItems = data.Modified.Where(x => x.ModifyTypes.Contains(SchedullerModifyTypes.ScheduleDeactivated));
                VerifyIfSwitchChanged(errorCode, VerifySwitchTypes.IfDeactivation, deActivatedItems, (changedItem, schGuid) =>
                {
                    _postService.DeActivatePost(schGuid.SchId, saveChanges: false);
                });

                // تغییرات را تا اینجا ذخیره میکنم
                // تا در مرحله بعد یک واکشی مجدد از پایگاه روی زمانبندی‌های کم و زیاد شده انجام دهیم
                // و روی نتایج آن به مرتب کردن اولویت زمانبندی‌ها بپردازیم
                _uow.SaveChanges();

                //ــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــــ
                var movedItems = data.Modified.Where(x => x.ModifyTypes.Contains(SchedullerModifyTypes.PriorityChanged)).ToList();
                var cpList_Of_MovedItems = _contentPackService.GetContentPacks(data.Modified.Select(x => HxUtility.CpCodeToCpId(x.CpId)).ToArray(), includePosts: true);

                // اعمال تغییرات مربوط به فرمان تغییر ترتیب انتشار محتواهاست
                foreach (var movedItemCp in cpList_Of_MovedItems)
                {
                    foreach (var schItem in movedItemCp.Posts)
                    {
                        var priority = movedItems.FirstOrDefault(x => x.CpId == movedItemCp.ContentPackCode)?.CpIndex??1;
                        _postService.ChangePriority(schItem, priority, saveChanges: false);
                    }
                }

                _uow.SaveChanges();
            }
            catch (Exception ex)
            {
                throw HxException.Generate(ex, errorCode);
            }
        }

        [NonAction]
        private ResultWithDetail<List<Post>, Dictionary<int, int>> SaveRemovedSchedules(string sendersErrorCode, long curUserId, DtoScheduler_SaveDataPackViewModel data, DateTime utcNow)
        {
            var errorCode = CodeOfMethods.WEB_SCHEDULE_CONTROLLER_SAVE_REMOVED_SCHEDULES.MakeErrorCode(CodeOfProjects.Web, sendersErrorCode);
            try
            {
                var portCount = new Dictionary<int, int>();
                List<Post> removedPosts = new List<Post>();

                if (data.Removed == null)
                {
                    return new ResultWithDetail<List<Post>, Dictionary<int, int>>(removedPosts, portCount);
                }

                var mainList = new List<DtoScheduler_ScheduleItemViewModel>();
                // تجمیع تمام لیست‌های زمانبندی موجود در تمام آیتم‌های لیست حذف شده‌ها در کنار هم در یک لیست
                mainList = data.Removed.Select(x => x.Schedules).Aggregate(mainList, (current, schList) => current.Concat(schList).ToList());
                foreach (var schItem in mainList)
                {
                    var removedPost= _postService.DeActivatePost(schItem.SchId);
                    removedPosts.Add(removedPost);

                    // پرکردن دیکشنری نتیجه
                    if (portCount.ContainsKey(removedPost.PortId))
                    {
                        portCount[removedPost.PortId] = portCount[removedPost.PortId] + 1;
                    }
                    else
                    {
                        portCount.Add(removedPost.PortId, 1);
                    }
                }
                _uow.SaveChanges();

                return new ResultWithDetail<List<Post>, Dictionary<int, int>>(removedPosts, portCount);
            }
            catch (Exception ex)
            {
                throw HxException.Generate(ex, errorCode);
            }
        }

        [NonAction]
        private void VerifyIfSwitchChanged(string sendersErrorCode, VerifySwitchTypes verifySwitchType, IEnumerable<DtoScheduler_ModifiedItemViewModel> targetItems, Action<DtoScheduler_ModifiedItemViewModel, DtoScheduler_ScheduleItemViewModel> targetAction)
        {
            var errorCode = CodeOfMethods.WEB_SCHEDULE_CONTROLLER_VERIFY_IF_SWITCH_CHANGED.MakeErrorCode(CodeOfProjects.Web, sendersErrorCode);
            try
            {
                if (verifySwitchType == VerifySwitchTypes.IfActivated)
                {
                    foreach (var changedItem in targetItems)
                    {
                        var origSchArray = changedItem.OrigSchedules.Select(x => x.SchId).ToArray();
                        foreach (var schItem in changedItem.Schedules)
                        {
                            // اگر سوئیچی روشن شده که هنگام لود صفحه روشن نبوده 
                            if (!origSchArray.Contains(schItem.SchId))
                            {
                                //------------ Modify the exists content schedule--------------------------------------

                                targetAction(changedItem, schItem);
                            }
                        }
                    }
                }
                else
                {
                    foreach (var changedItem in targetItems)
                    {
                        var schArray = changedItem.Schedules.Select(x => x.SchId).ToArray();
                        foreach (var origSchItem in changedItem.OrigSchedules)
                        {
                            // اگر سوئیچ روشن شده بوده در هنگام لود صفحه بعدا خاموش شده 
                            if (!schArray.Contains(origSchItem.SchId))
                            {
                                //------------ Modify the exists content schedule--------------------------------------

                                targetAction(changedItem, origSchItem);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw HxException.Generate(ex, errorCode);
            }
        }

        [NonAction]
        private async Task<Post> AddContentForTargetSocialNetworkAsync(string sendersErrorCode, Brand brand, long publisherUserId, ContentPack cp,
            Port port, PortAdmin portAdmin, int priority, DateTime publishDtAsUtc,
            HxSocialNetworks socialNetworkId, DateTime utcNow)
        {
            var errorCode = CodeOfMethods.WEB_SCHEDULE_CONTROLLER_ADD_CONTENT_FOR_TARGET_SOCIAL_NETWORK_ASYNC.MakeErrorCode(CodeOfProjects.Web, sendersErrorCode);
            try
            {
                //Get telegram version of the content
                var co = await _contentManager.GetContent(cp.ContentPackId, socialNetworkId);

                //Make new content for the target social network if require (for all except telegram) in db
                co = _contentManager.AddContent(co, socialNetworkId);

                //------------ Add new content schedule-----------------------------------------------
                var vModel = new AddPostViewModel
                {
                    BrandId = brand.BrandId,
                    PublishDt = publishDtAsUtc,
                    ContentPackId = cp.ContentPackId,
                    ContentId = co.ContentId,
                    ContentPackTitle = cp.Title,
                    ContentTypeId = cp.ContentTypeId,
                    Priority = priority,
                    SocialNetworkId = (int) socialNetworkId,
                    PortId = port.PortId,
                    //PortIdentity = port.PortIdentity,
                    //PortUsername = port.PortUsername,
                    PortTypeId = port.PortTypeId,
                    BrandOwnerUserId = brand.OwnerUserId.Value,
                    PublisherPortAdminId = portAdmin.PortAdminId,
                    PublisherUserId = publisherUserId,
                    PublishStatusId = HxPublishStatuses.ToDo,
                    PublishStatusModifiedOn = utcNow,
                    IsPublishApproved = true,
                    PublishApproverUserId = brand.OwnerUserId.Value
                };
                var result= _postService.Add(vModel);
                return result;
            }
            catch (Exception ex)
            {
                throw HxException.Generate(ex, errorCode);
            }
        }
        

        /// <summary>
        /// این متد برند‌هایی از کاربر را برگشت می‌دهد که دست کم یک درگاه آن برند در وضعیت فعال باشد
        /// اگر هیچ یک از درگاه‌های یک برند در شبکه‌های اجتماعی مختلف 
        /// در وضعیت فعال نباشند آنگاه آن برند در لیست برگشتی قرار داده نخواهد شد
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        //private List<int> GetAllBrandsOfUserThatHaveAtlastOneAssignedPortToSechedulingServ(long userId)
        //{
        //    // بدست آوردن پورت‌هایی از کاربر که به سرویس زمانبندی منتسب شده‌اند
        //    var assignedPortsOfUser_ToSchedulingServ = _consManager.GetAssignedPortsOfUser(CurUserId, HxServTypes.Posting);
        //    // برندهایی از کاربر که دستکم یکی از پورت‌هایش به سرویس زمانبندی منتسب شده است
        //    var allowedBrands = assignedPortsOfUser_ToSchedulingServ.Select(x => x.BrandId).Distinct().ToList();
        //    return allowedBrands;
        //}
        #endregion



    }

    

    internal enum VerifySwitchTypes
    {
        IfActivated,
        IfDeactivation
    }

    

    


}
