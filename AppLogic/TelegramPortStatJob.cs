using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DelegateDecompiler;
using Helperx.DomainClasses.Entities.Common.Enums;
using Helperx.ServiceLayer.Contracts;
using Helperx.Trigger.BL.Inf;
using Helperx.Utility;
using Helperx.ViewModel.Areas.ControlPanel.TelegramPortStats;
using Helperx.Webhook.HelperxBotsShare.Contracts;
using Nicshell.TelegramUtility.Common.Model;
using Nicshell.TelegramUtility.Web;
using Quartz;
using Telegram.Bot;

namespace Helperx.Trigger.BL.Jobs
{
    //این اتریبیوت نمیگذارد این جاب دوباره اجرا شود وقتی هنوز اجرای قبلی اش تمام نشده
    [DisallowConcurrentExecution]
    public class TelegramPortStatJob : IJob
    {
        private string _statGroupKey;
        public ITelegramPortStatService TelegramPortStatService { get; set; }

        public ITelegramPortService TelegramPortService { get; set; }

        public TelegramBotClient Bot { get; set; }

        public IHelperxRoBotService BotService { get; set; }

        public async Task Execute(IJobExecutionContext context)
        {
            Console.WriteLine();
            Console.Write(@"Job fired   »   Telegram Port Stat");
            Console.Write(@"    |    ");
            Console.WriteLine(@"Time: " + DateTime.UtcNow.ToLongTimeString());

            _statGroupKey = KeyGenerator.GetUniqueKey(10);

            if (Bot == null)
            {
                Console.Write(@"Error: The Bot not Found!");
            }

            var utcNow = DateTime.UtcNow;
            var portList = new List<BodyItem>();
            
            //==================================================================================================================
            // واکشی پورتها
            try
            {
                // بدست آوردن لیست پورتهایی که باید آمارشان را بگیریم
                portList =
                    TelegramPortService.GetTelegramPorts(false)
                    .Where(x => 
                    // اگر پورتش میتونه آمارگیری بشه
                    x.CanStat 
                    // اگر پورتش شناسه-تلگرامی یا یوزرنیم داره
                    && (x.Identifier != null || x.Username != null)) // << فقط پورت‌هایی را بیار که از شواهد پیداست که قابلیت آمارگیری دارند
                    .Select(x => new BodyItem
                        {
                        TelegramPortId = x.TelegramPortId,
                        Identifier = (long)x.Identifier,
                        Username = x.Username,
                        AccessHash = x.AccessHash
                    })
                    .Decompile()
                    .ToList();

                Console.WriteLine($@"Fetched ports to stat: {portList.Count}");
                await BotService.NotifyAdmin_TgPortStatJob_StartedAsync(_statGroupKey, portList.Count);
            }
            catch (Exception ex)
            {
                Inf.Utility.WriteError(ex, GetType().Name, MethodBase.GetCurrentMethod().Name, "Khata dar vakeshi portha");
            }

            //==================================================================================================================
            // دسته-دسته کردن پورتها در تسک‌های موازی و اجرای موازی آنها
            try
            {
                    if (portList.Count > 0)
                    {
                        // تعداد پپورتهای هر دسته
                        var number_Of_Ports_Per_Page = new decimal(10000);
                        // تعداد دسته‌ها
                        var groupdTaskListCount = (int)Math.Ceiling(portList.Count() / number_Of_Ports_Per_Page);

                        // لیست تسک‌ها
                        var taskList = new List<Task>();
                        // پر کردن لیست تسک‌ها
                        for (var i = 0; i < groupdTaskListCount; ++i)
                        {
                            // واکشی پورت‌های این تسک
                            var curPortList = portList
                                .Skip((int)number_Of_Ports_Per_Page * i)
                                .Take((int)number_Of_Ports_Per_Page)
                                .ToList();

                            // ایجاد پارامترهای بدنه این تسک
                            var bodyParams = new BodySetting(
                                // لیست  پورتهای این تسک
                                curPortList.ToList()
                                // اگر روش وب شکست خورد، روش بات تلاش شود
                                , tryByBotIfPossible_WhenWebWayFailed: true
                                // مکث قبل از شروع تسک
                                , delayBeforDoingBody: TimeSpan.Zero
                                // مکث قبل از آمارگیری با روش وب
                                , delayBeforDoingPerBodyItem_ByWeb: TimeSpan.FromSeconds(1.8)
                                // مکث قبل از آمارگیری با روش بات
                                , delayBeforDoingPerBodyItem_ByBot: TimeSpan.FromSeconds(10.0));

                            // تنظیمات سعی مجدد این تسک
                            // برای مواردی که شکست می‌خوردند
                            var retrySettings_4FailedItems = new RetrySettings(new List<TimeSpan>
                            {
                                TimeSpan.FromMinutes(10.0)
                            });

                            // قرار دادن فرمان اجرایی این تسک در لیست تسک‌ها
                            taskList.Add(DoBodyProccessAsync(bodyParams, retrySettings_4FailedItems));
                        }

                        // اگر لیست تسکها خالی نبود
                        if (taskList.Any())
                        {
                            // فرمان اجرای موازی تمام تسک‌های داخل لیست تسک‌ها
                            await Task.WhenAll(taskList);
                            
                            // نوتیفای به مدیران هلپریکس راجع به خاتمه یافتن فرآیند آمارگیری
                            await BotService.NotifyAdmin_TgPortStatJob_FinishedAsync(_statGroupKey);
                        }
                    }
                }
                catch (Exception ex)
                {
                Inf.Utility.WriteError(ex, GetType().Name, MethodBase.GetCurrentMethod().Name, "khata dar daste-daste kardan va task bandi port ha va ejraye movazieshan.");
            }

            //if (portList.Count <= 0)
            //    {
            //        return ;
            //    }

            //    //var schedulesOfPorts = schList.GroupBy(x => x.ChatId).ToList();
            //    //decimal number_Of_Ports_Per_Page = 500;// 50M; 

            //    // روند به بالا: 1.02 را بده 2
            //    //var groupdTaskListCount = (int)Math.Ceiling(portList.Count() / number_Of_Ports_Per_Page);

            //    //------------------------------------------------------------------------------
            //    // ایجاد استر تردها
            //    ThreadPool.SetMaxThreads(1, 1);//TODO فعلا یک ترد باز میکنیم

            //    //------------------------------------------------------------------------------
            //    // ارسال پست‌های هر کانال در یک ترد مجزا که از استخر تردها قرض میگیریم
            //    for (var i = 0; i < groupdTaskListCount; i = i + 1)
            //    {
            //        // واکشی
            //        var pageTasks = portList
            //            .Skip((int)number_Of_Ports_Per_Page * i)
            //            .Take((int)number_Of_Ports_Per_Page)
            //            .ToList();
            //        // اجرای ترد
            //        ThreadPool.QueueUserWorkItem(new WaitCallback(input =>
            //            MyThread_ForEachGroupOfTgPorts(input)),
            //            new BodyParam(pageTasks.ToList()
            //                , delayBeforDoingBody_Milisecond: 1000,
            //                // todo این مکث رو ایجاد میکنیم تا خطای 429 از تلگرام بابت ارسال پیام‌های مکرر زیر 1 ثانیه نگیریم. شاید لازم باشه کم و زیادش کنیم. داکیومنت تلگرام خوانده شود
            //                delayBeforDoingPerBodyItem_Milisecond: 5000));

            //        //todo      429 جلوگیری از خطای  👆
            //        //todo      1) When sending messages inside a particular chat, avoid sending more than 1 message per second. 
            //        //todo         We may allow short bursts that go over this limit, but eventually you'll begin receiving 429 errors.

            //        //todo      2) If you're sending bulk notifications to multiple users, the API will not allow more than 30 messages per second or so.
            //        //todo         Consider spreading out notifications over large intervals of 8—12 hours for best results.

            //        //todo      3) Also note that your bot will not be able to send more than 20 messages per minute to the same group.
            //        //https://core.telegram.org/bots/faq#my-bot-is-hitting-limits-how-do-i-avoid-this
            //    }
            //}
            //catch (Exception ex)
            //{
            //    Inf.Utility.WriteError(ex, GetType().Name, MethodBase.GetCurrentMethod().Name);
            //}
            //return Task.FromResult(true);
        }







        private async Task DoBodyProccessAsync(BodySetting bodySetting, RetrySettings retrySettings_4FailedItems)
        {
            if (bodySetting == null)
                return;
            var bodyResult1 = await DoBodyAsync(bodySetting);
            var bodyResult2 = bodyResult1;
            await SaveBodyAsync(bodySetting, bodyResult2, retrySettings_4FailedItems);
        }

        private async Task<BodyResult<int, int,BodyFailedItem>> DoBodyAsync(BodySetting bodySettingses)
        {
            Console.WriteLine();
            Console.WriteLine(@"########### Start Of Body OF Stat ############");
            Console.WriteLine();
            Console.WriteLine(@"Body of PortStat job fired.    |    Utc Time: {0}", DateTime.UtcNow.ToLongTimeString());
            Console.WriteLine(@"Params:");
            Console.WriteLine(@"Count of items:    {0}", bodySettingses.Items.Count);
            Console.WriteLine(@"Delay before all items:    {0}ms", bodySettingses.DelayBeforDoingBody);
            Console.WriteLine(@"Delay before each item:    {0}ms", bodySettingses.DelayBeforDoingPerBodyItem_ByWeb);
            Console.WriteLine();
            Console.WriteLine(@"##############################################");
            Console.WriteLine();

            // مکث قبل از انجام بدنه
            await Task.Delay(bodySettingses.DelayBeforDoingBody);

            // لیست موفق‌ها و شکست خورده‌ها
            var successList = new Dictionary<int, int>();
            var failedList = new List<BodyFailedItem>();

            // حلقه روی آیتم‌های بدنه
            foreach (var bodyItem in bodySettingses.Items.ToList())
            {
                var bodyFailedItem = new BodyFailedItem(bodyItem);
                try
                {
                    // آیا این پورت آیدنتیفایر معتبر دارد؟
                    var hasValid_Identifier = bodyItem.Identifier.HasValue && bodyItem.Identifier.GetValueOrDefault()!=0;
                    // آیا این پورت نام‌کاربری معتبر دارد؟
                    var hasValid_Username = !string.IsNullOrEmpty(bodyItem.Username);

                    // اگر نام‌کاربری یا آیدنتیفایر معتبر داشت
                    if (hasValid_Identifier || hasValid_Username)
                    {
                        // تعداد فالور با مقدار پیش‌فرض آن
                        var portFollowerCount = -1;

                        // اگر نام‌کاربر معتبر دارد
                        if (hasValid_Username)
                        {
                            // مکث قبل از آمارگیری با روش وب
                            await Task.Delay(bodySettingses.DelayBeforDoingPerBodyItem_ByWeb);

                            // خواندن صفحه وب این کانال از روی سایت تلگرام و استخراج اطلاعات از آن
                            TgWebFetch usernameWebPage = TgWebTools.FetchTelegramUserInfoByWeb(bodyItem.Username, -1);

                            // اگر استخراج موفق بود
                            if (usernameWebPage.IsWebFetched)
                            {
                                // اگر نوع کاربر استخراجیکانال عمومی بود 
                                if (usernameWebPage.UsernameType == UsernameTypes.Channel)
                                {
                                    portFollowerCount = usernameWebPage.FollowerCount;
                                }
                                else
                                {
                                    // علامت‌گذاری اینکه این آیتم قابل آمارگیری از روش وب نبود
                                    bodyFailedItem.IsNotStatable = true;
                                    // تنظیم نوع نام‌کاربری این کانال
                                    bodyFailedItem.UsernameType = usernameWebPage.UsernameType;
                                }
                            }
                        }

                        if (bodySettingses.TryByBotIfPossible_WhenWebWayFailed &&
                            // اگر آیدنتیفایر معتبر دارد
                            hasValid_Identifier &&
                            // اگر آمارش هنوز رقم منفی (پیش‌فرض قبل از آمارگیری) را نشان میدهد
                            portFollowerCount < 0 &&
                            // اگر علامت غیر‌قابل آمارگیری ندارد
                            !bodyFailedItem.IsNotStatable)
                        {
                            // نکث قبل از آمارگیری به روش بات
                            await Task.Delay(bodySettingses.DelayBeforDoingPerBodyItem_ByBot);

                            var bot = Bot;
                            var chatId = bodyItem.Identifier.Value;
                            portFollowerCount = await bot.GetChatMembersCountAsync(chatId);
                        }

                        // اگر آمار این کانال هنوز رقم منفی (پیش‌فرض زمان شکست در روش‌های آمارگیری بالا) را نشان میدهد
                        if (portFollowerCount < 0)
                        {
                            // افزودن آیتم جاری به لیست شکست خورده‌ها
                            failedList.Add(bodyFailedItem);
                        }
                        // اگر فالور بزرگتر از 0 داشت
                        else if (portFollowerCount > 0)
                        {
                            // افزودن این آیتم به لیست موفق شده‌ها
                            successList.Add(bodyItem.TelegramPortId, portFollowerCount);
                        }
                        Console.WriteLine();
                        Console.Write($@"Follower count of Port #" + bodyItem.TelegramPortId + " >> " + portFollowerCount);
                        Console.Write(@"    |    ");
                        Console.WriteLine(@"Utc Time: " + DateTime.UtcNow.ToLongTimeString());
                        Console.WriteLine(@"______________________________________________");
                    }
                }
                catch (Exception ex)
                {
                    failedList.Add(bodyFailedItem);
                    Console.WriteLine(bodyItem.TelegramPortId + " >> " + bodyItem.Username.AddFirstAtsign(false));
                    Inf.Utility.WriteError(ex, GetType().Name, MethodBase.GetCurrentMethod().Name, "Th error occured on BODY of get Telegram follower count!");
                }
            }

            var bodyResult = new BodyResult<int, int, BodyFailedItem>(successList, failedList);
            Console.WriteLine();
            Console.WriteLine(@"############ End Of Body OF Stat ############");
            Console.WriteLine();
            Console.WriteLine(@"Body done.    |    Utc Time: {0}", DateTime.UtcNow.ToLongTimeString());
            Console.WriteLine(@"Result:");
            Console.WriteLine(@"Total counts : " + bodySettingses.Items.Count);
            Console.WriteLine(@"Successed Count: :" + bodyResult.SuccessList.Count);
            Console.WriteLine(@"Failed Count: :" + bodyResult.FailedList.Count);
            Console.WriteLine();
            Console.WriteLine(@"##############################################");
            Console.WriteLine();
            return bodyResult;
        }

        private async Task SaveBodyAsync(BodySetting bodySetting, BodyResult<int,int, BodyFailedItem> bodyResult, RetrySettings retrySettings_4FailedItems)
        {
            Console.WriteLine();
            Console.WriteLine("#################### Saving Stats ####################");
            Console.WriteLine();
            Console.WriteLine("Total counts : " + bodySetting.Items.Count);
            Console.WriteLine("Successed Count: :" + bodyResult.SuccessList.Count);
            Console.WriteLine("Failed Count: :" + bodyResult.FailedList.Count);
            Console.WriteLine();
            Console.WriteLine("######################################################");
            Console.WriteLine();
            var utcNow = DateTime.UtcNow;


            //===================================================================================================================================================================================
            // TelegramPortStat تلاش برای اضافه کردن آمار فالور کانال‌هایی که آمارگیری شدند به جدول
            try
            {
                // ذخیره سازی دسته جمعی موفق‌ها
                TelegramPortStatService.BatchAdd(bodyResult.SuccessList.Select(x => new AddPortStatViewModel
                {
                    TelegramPortId = x.Key,
                    FollowerCount = x.Value,
                    Createdn = utcNow
                }).ToList());
                
                // نوتیفای راجع به موفق‌ها به مدیران هلپریکس
                await BotService.NotifyAdmin_TgPortStatJob_SuccessItems_ThatSavedSuccessfullyAsync(_statGroupKey, bodySetting.Items.Count, bodyResult.SuccessList.Count);
            }
            catch (Exception ex)
            {
                Inf.Utility.WriteError(ex, GetType().Name, MethodBase.GetCurrentMethod().Name, "Err Code: 1001- The error occured on BulkInsert > Saving new follower counts.");
                // افزودن موفقهایی که در زمان ثبت با مشکل روبرو شدند به لیست شکست‌خورده‌ها
                bodyResult.FailedList.AddRange(bodySetting.Items.Where(x => bodyResult.SuccessList.Select(y => y.Key).Contains(x.TelegramPortId)).Select(x => new BodyFailedItem(x)).ToList());
                // نوتیفای به مدیران هلپریکس درباره خطای ایجاد شده
                await BotService.NotifyAdmin_TgPortStatJob_SuccessItems_ThatFailedToSaveAsync(_statGroupKey, bodySetting.Items.Count, bodyResult.SuccessList.Count);
            }
           
            //===================================================================================================================================================================================
            // TelegramPort تلاش برای آپدیت کردن تعدادفالور کانالهایی که آمارگیری شدند در جدول
            try
            {
                var dic_TelegramPortId_ChangedFields = bodyResult.SuccessList.ToDictionary(
                    successItem => successItem.Key,
                    successItem => new Dictionary<string, string>
                    {
                        {
                            "FollowerCount", successItem.Value.ToString()
                        },
                        {
                            "FollowerCountUpdateDt", $"'{utcNow:yyyy-MM-dd hh:mm:ss}'" //    '2012-10-20 22:40:30'   مثال:
                        },
                        {
                            "NoStat", "0"
                        }
                    });
                
                TelegramPortService.ChangeMany(dic_TelegramPortId_ChangedFields, "TelegramPortId", "TelegramPort");
                // اعلان به مدیران هلپریکس درباره پورتهای آمارگیری شده موفق که با موفقیت در پایگاه وضعیتشان ثبت شد
                await BotService.NotifyAdmin_TgPortStatJob_SuccessItems_ThatUpdateSuccessfullyFollowerCountsInTelegramPortAsync(_statGroupKey, bodySetting.Items.Count, bodyResult.SuccessList.Count);
            }
            catch (Exception ex)
            {
                // TelegramPort اعلان خطا به مدیران هلپریکس درباره ایجاد خطا در فرآیند آپدیت آمار موارد موفق در جدول
                await BotService.NotifyAdmin_TgPortStatJob_SuccessItems_ThatUpdateFollowerCountsInTelegramPortFailedAsync(_statGroupKey, bodySetting.Items.Count, bodyResult.SuccessList.Count);
                Inf.Utility.WriteError(ex, GetType().Name, MethodBase.GetCurrentMethod().Name, "Err Code: 1002");
            }
            
            var isLastTry = false;
            BodyResult<int, int, BodyFailedItem> bodyResult_OfCurrentRetry = null;

            // حلقه مربوط به تلاشهای مجدد
            foreach (var retry in retrySettings_4FailedItems.Retries.Where(x => !x.Finished))
            {
                if (bodyResult.FailedList.Count <= 0)
                    return;

                // مکث قبل از تلاش مجدد
                await Task.Delay(retry.Delay_BeforeRetry);
                

                try
                {
                    // اجرای بدنه
                    bodyResult_OfCurrentRetry = await DoBodyAsync(new BodySetting(bodyResult.FailedList.Where(x => x.IsNotStatable).Cast<BodyItem>().ToList(), false, bodySetting.DelayBeforDoingBody, bodySetting.DelayBeforDoingPerBodyItem_ByWeb, bodySetting.DelayBeforDoingPerBodyItem_ByBot));
                    isLastTry = false;

                    // اگر یک تلاش خاتمه نیافته بیشتر نداشتیم
                    // یعنی در همان تلاشیم و میرویم برای ذخیره نتایج
                    if (retrySettings_4FailedItems.HasJust_One_UnFinished())
                    {
                        isLastTry = true;
                        Console.WriteLine(@"==== >> Mark {0} Port as IsNotStatable", bodyResult_OfCurrentRetry.FailedList.Count(x => x.IsNotStatable));
                        UpdateTelegramPort_ByFailedList(bodyResult_OfCurrentRetry.FailedList, UsernameTypes.Bot);
                        UpdateTelegramPort_ByFailedList(bodyResult_OfCurrentRetry.FailedList, UsernameTypes.User);
                        UpdateTelegramPort_ByFailedList(bodyResult_OfCurrentRetry.FailedList, UsernameTypes.Channel);
                        UpdateTelegramPort_ByFailedList(bodyResult_OfCurrentRetry.FailedList, UsernameTypes.Unknow);
                        
                        // اعلان به مدیران هلپریکس درباره شکست خورده‌هایی که با موفقیت ثبت وضعیت شدند در پایگاه
                        await BotService.NotifyAdmin_TgPortStatJob_FailedItems_WithNotStatable_ThatSavedSuccessfullyAsync(_statGroupKey, bodySetting.Items.Count, bodyResult.FailedList.Count(x => x.IsNotStatable));
                    }
                    // پایان یافته نامیدن این تلاش
                    retry.Finish();
                    
                    // اگر در آخرین تلاشیم
                    if (!isLastTry)
                    {
                        await SaveBodyAsync(bodySetting, bodyResult_OfCurrentRetry, retrySettings_4FailedItems);
                    }
                }
                catch (Exception ex)
                {
                    Inf.Utility.WriteError(ex, GetType().Name, MethodBase.GetCurrentMethod().Name, "The error occured on RETRY SaveBody of Telegram follower count.");
                    // اعلان به مدیران هلپریکس درباره ایجاد خطا در فرایند ثبت شکست‌خورده‌هایی که میخواستیم در پایگاه وضعیتشان را ثبت کنیم
                    await BotService.NotifyAdmin_TgPortStatJob_FailedItems_WithNotStatable_ThatFailedToSaveAsync(_statGroupKey, bodySetting.Items.Count, bodyResult.FailedList.Count(x => x.IsNotStatable));
                }
            }
            if (!isLastTry || bodyResult_OfCurrentRetry == null)
                return;

            var countOf_FailedItems_ByBot = bodyResult_OfCurrentRetry.FailedList.Count(x =>x.Identifier.HasValue && !x.IsNotStatable);

            // اعلان به مدیران هلپریکس راجع به شکست خورده‌ها
            await BotService.NotifyAdmin_TgPortStatJob_FailedItems_ByBotAsync(
                _statGroupKey
                , bodySetting.Items.Count
                , bodyResult.FailedList
                    .Where(x => !x.IsNotStatable)
                    .Select(x => x.TelegramPortId)
                    .ToList()
            );
        }

        private void UpdateTelegramPort_ByFailedList(List<BodyFailedItem> failedList, UsernameTypes usernameType)
        {
            TelegramPortService.BulkChangeStat(
                failedList
                    .Where(x =>
                        x.IsNotStatable &&
                        x.UsernameType == usernameType)
                    .Select(x => x.TelegramPortId)
                    .ToList()
                , false
                , Username2PortType(usernameType), "IsNotStatable");
        }

        private static HxPortTypes Username2PortType(UsernameTypes usernameType)
        {
            switch (usernameType)
            {
                case UsernameTypes.Channel:
                    return HxPortTypes.TelegramPublicChannel;
                case UsernameTypes.User:
                    return HxPortTypes.TelegramUser;
                case UsernameTypes.Bot:
                    return HxPortTypes.TelegramBot;
                case UsernameTypes.Unknow:
                    return HxPortTypes.Unknow;
                default:
                    return HxPortTypes.Unknow;
            }
        }





























        //private async void MyThread_ForEachGroupOfTgPorts(object input)
        //{
        //    var bodyParam = input as BodyParam;
        //    if (bodyParam == null)
        //        return;
        //    //_____________________________________________________________DO Body & Save Result__________________________________________________________________________________

        //    // Do body
        //    var bodyResult = await DoBodyAsync(bodyParam);

        //    // Save body result
        //    SaveBody(bodyParam, bodyResult, saveFaildItems: false);

        //    //________________________________________________________RETRY SAVE BODY FOR FAILD ITEMS_____________________________________________________________________________
        //    try
        //    {
        //        // تلاش مجدد برای شکست‌خورده‌ها
        //        if (bodyResult.FailedList.Count <= 0)
        //            return;

        //        // میزان مکث قبل از اجرای بدنه
        //        var delay_Befor_Doing_Body_Milisecond = (int)TimeSpan.FromMinutes(5).TotalMilliseconds;
        //        // میزان مکث قبل از اجرای هر آیتم بدنه
        //        const int DELAY_BEFOR_DOING_PER_BODY_ITEM_MILISECOND = 600;

        //        // حالا بدنه اصلی جاب را اجرا میکنیم ولی دستور میدهیم که قبل از اجرای کل آن و همچنین قبل از هر آیتم آن مقداری مکث لحاظ شود
        //        // تلاش داریم با این مکث‌ها استرس را کاسته و موفقیت جاب را بالا ببریم
        //        var retryBodyResult = await DoBodyAsync(new BodyParam(bodyResult.FailedList, delay_Befor_Doing_Body_Milisecond, DELAY_BEFOR_DOING_PER_BODY_ITEM_MILISECOND));

        //        SaveBody(bodyParam, retryBodyResult, saveFaildItems: true);
        //    }
        //    catch (Exception ex)
        //    {
        //        Inf.Utility.WriteError(ex, GetType().Name, MethodBase.GetCurrentMethod().Name, "The error occured on RETRY SaveBody of Telegram follower count.");
        //        // todo:  آیا در این هنگام گزارش گیری خاصی میشود انجام داد یا وضعیت بحرانی به مدیر سایت اطلاع داده شود
        //    }

        //}

        //private void SaveBody(BodyParam bodyParam, BodyResult<KeyValuePair<int, int>, BodyItem> body, bool saveFaildItems)
        //{
        //    try
        //    {
        //        Console.WriteLine();
        //        Console.WriteLine("#################### Start ####################");
        //        Console.WriteLine();
        //        Console.WriteLine("Saving new follower counts.");
        //        Console.WriteLine("Successed Count: :" + body.SuccessList.Count);
        //        Console.WriteLine("Failed Count: :" + body.FailedList.Count);
        //        Console.WriteLine();
        //        Console.WriteLine("#################### Start ####################");
        //        Console.WriteLine();

        //        var utcNow = DateTime.UtcNow;
        //        // ذخیره سازی موفق‌ها
        //        TelegramPortStatService.BatchAdd(
        //            body.SuccessList.Select(x => new AddPortStatViewModel
        //            {
        //                TelegramPortId = x.Key,
        //                FollowerCount = x.Value,
        //                Createdn = utcNow
        //            }).ToList()
        //            );

        //        // ذخیره سازی شکست خورده‌ها درصورت نیاز
        //        if (saveFaildItems)
        //        {
        //            //todo ?????? 
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Inf.Utility.WriteError(ex, GetType().Name, MethodBase.GetCurrentMethod().Name, "The error occured on BulkInsert > Saving new follower counts.");
        //        // todo:  آیا در این هنگام گزارش گیری خاصی میشود انجام داد یا وضعیت بحرانی به مدیر سایت اطلاع داده شود

        //        //در اینجا آیتم‌های موفق انجام شده نتوانسته‌اند در پایگاه ذخیره شوند
        //        //لذا آنها را هم به لیست شکست خورده‌ها اضافه میکنیم
        //        body.FailedList.AddRange(bodyParam.Items.Where(x => body.SuccessList.Select(y => y.Key).Contains(x.TelegramPortId)).ToList());
        //    }

        //}

        //private async Task<BodyResult<KeyValuePair<int, int>, BodyItem>> DoBodyAsync(BodyParam bodyParams) //)where  TBodyParamItem : PortStatItem
        //{
        //    Console.WriteLine();
        //    Console.WriteLine("#################### Start ####################");
        //    Console.WriteLine();
        //    Console.WriteLine($"Body of PortStat job fired.    |    Utc Time: {DateTime.UtcNow.ToLongTimeString()}");
        //    Console.WriteLine("Params:");
        //    Console.WriteLine($"Count of items:    {bodyParams.Items.Count}");
        //    Console.WriteLine($"Delay before all items:    {bodyParams.DelayBeforDoingBody_Milisecond}ms");
        //    Console.WriteLine($"Delay before each item:    {bodyParams.DelayBeforDoingPerBodyItem_Milisecond}ms");
        //    Console.WriteLine();
        //    Console.WriteLine("##############################################");
        //    Console.WriteLine();

        //    // مکث قبل از انجام کل کار
        //    Thread.Sleep(bodyParams.DelayBeforDoingBody_Milisecond);

        //    // لیستی از شناسه‌های پورت-تلگرامی و تعداد عضو آن
        //    var successList = new List<KeyValuePair<int, int>>();
        //    //var failedList = new List<long>();
        //    var failedList = new List<BodyItem>();

        //    foreach (var ps in bodyParams.Items.ToList())
        //    {
        //        try
        //        {
        //            if (ps.Identifier == 0)
        //            {
        //                continue;
        //            }

        //            // مکث قبل از انجام کار این آیتم
        //            Thread.Sleep(bodyParams.DelayBeforDoingPerBodyItem_Milisecond);

        //            //var portId = await Bot.GetChatAsync(ps.ChanelUsername);

        //            //var portFollowerCount = await Bot.GetChatMembersCountAsync(ps.Identity);

        //            var portFollowerCount = -1;

        //            //TODO روش وب:
        //            if (!string.IsNullOrEmpty(ps.Username))
        //            {
        //                portFollowerCount = GetFollowerCount_ByWeb(ps.Username);
        //            }
        //            //TODO روش ربات :
        //            else
        //            {
        //                portFollowerCount = await Bot.GetChatMembersCountAsync(ps.Identifier);

        //                // افزودن این مورد به لیست آمار پورت‌ها
        //                successList.Add(new KeyValuePair<int, int>(ps.TelegramPortId, portFollowerCount));
        //            }

        //            // برای تعداد فالورهای منفی و صفر اقدام به ثبت اطلاعات نمی‌کنیم
        //            if (portFollowerCount > 0)
        //            {
        //                // افزودن این مورد به لیست آمار پورت‌ها
        //                successList.Add(new KeyValuePair<int, int>(ps.TelegramPortId, portFollowerCount));
        //            }
        //            else
        //            {
        //                portFollowerCount = await Bot.GetChatMembersCountAsync(ps.Identifier);
        //                if (portFollowerCount > 0)
        //                {
        //                    // افزودن این مورد به لیست آمار پورت‌ها
        //                    successList.Add(new KeyValuePair<int, int>(ps.TelegramPortId, portFollowerCount));
        //                }
        //            }


        //            Console.WriteLine();
        //            Console.Write("Follower count of Port #" + ps.TelegramPortId + " >> " + portFollowerCount);
        //            Console.Write("    |    ");
        //            Console.WriteLine("Utc Time: " + DateTime.UtcNow.ToLongTimeString());
        //            Console.WriteLine("______________________________________________");
        //        }
        //        catch (Exception ex)
        //        {
        //            // افزودن این زمانبندی به لیست ارسال شده‌های ناموفق
        //            failedList.Add(ps);
        //            Console.WriteLine(ps.TelegramPortId + " >> " + ps.Username.AddFirstAtsign());
        //            Inf.Utility.WriteError(ex, GetType().Name, MethodBase.GetCurrentMethod().Name, "Th error occured on BODY of get Telegram follower count!");
        //        }
        //    }

        //    var body = new BodyResult<KeyValuePair<int, int>, BodyItem>(successList, failedList);

        //    Console.WriteLine();
        //    Console.WriteLine("#################### End ####################");
        //    Console.WriteLine();
        //    Console.WriteLine($"Body done.    |    Utc Time: {DateTime.UtcNow.ToLongTimeString()}");
        //    Console.WriteLine("Result:");
        //    Console.WriteLine("Successed Count: :" + body.SuccessList.Count);
        //    Console.WriteLine("Failed Count: :" + body.FailedList.Count);
        //    Console.WriteLine();
        //    Console.WriteLine("##############################################");
        //    Console.WriteLine();

        //    return body;
        //}

        //private int GetFollowerCount_ByWeb(string username)
        //{
        //    return TgWebTools.GetChatCompactFromTgWeb(username)?.FollowerCount ?? -1;
        //}

        
        //##########################################################################################################
        private class BodySetting
        {
            public BodySetting(List<BodyItem> items, bool tryByBotIfPossible_WhenWebWayFailed, TimeSpan delayBeforDoingBody, TimeSpan delayBeforDoingPerBodyItem_ByWeb, TimeSpan delayBeforDoingPerBodyItem_ByBot)
            {
                Items = items;
                DelayBeforDoingBody = delayBeforDoingBody;
                DelayBeforDoingPerBodyItem_ByWeb = delayBeforDoingPerBodyItem_ByWeb;
                DelayBeforDoingPerBodyItem_ByBot = delayBeforDoingPerBodyItem_ByBot;
                TryByBotIfPossible_WhenWebWayFailed = tryByBotIfPossible_WhenWebWayFailed;
            }

            public bool TryByBotIfPossible_WhenWebWayFailed { get; set; }

            public List<BodyItem> Items { get; private set; }

            public TimeSpan DelayBeforDoingBody { get; private set; }

            public TimeSpan DelayBeforDoingPerBodyItem_ByWeb { get; private set; }

            public TimeSpan DelayBeforDoingPerBodyItem_ByBot { get; set; }
        }

        private class BodyItem
        {
            public long? Identifier { get; set; }

            public string Username { get; set; }

            public int TelegramPortId { get; set; }

            public long? AccessHash { get; set; }
        }

        private class BodyFailedItem : BodyItem
        {
            public BodyFailedItem()
            {}

            public BodyFailedItem(BodyItem bodyItem)
            {
                TelegramPortId = bodyItem.TelegramPortId;
                AccessHash = bodyItem.AccessHash;
                Identifier = bodyItem.Identifier;
                Username = bodyItem.Username;
            }

            public bool IsNotStatable { get; set; }

            public UsernameTypes UsernameType { get; set; }
        }

        private class RetrySettings
        {
            public RetrySettings(IEnumerable<TimeSpan> listOfDelay_BeforeEachRetry)
            {
                Retries = listOfDelay_BeforeEachRetry.Select(x => new Retry(x)).ToList();
            }

            public List<Retry> Retries { get; private set; }

            public bool HasJust_One_UnFinished()
            {
                return Retries.Count(x => !x.Finished) == 1;
            }
        }

        private class Retry
        {
            public Retry(TimeSpan delay_BeforeRetry)
            {
                Delay_BeforeRetry = delay_BeforeRetry;
                RetryId = Guid.NewGuid();
            }

            public bool Finished { get; private set; }

            public Guid RetryId { get; private set; }

            public TimeSpan Delay_BeforeRetry { get; private set; }

            public void Finish()
            {
                Finished = true;
            }
        }
    }
}