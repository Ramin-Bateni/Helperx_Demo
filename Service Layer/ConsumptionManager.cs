using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using DelegateDecompiler;
using DelegateDecompiler.EntityFramework;
using Helperx.Common;
using Helperx.Common.Helpers;
using Helperx.DataLayer.Context;
using Helperx.DomainClasses.Entities.Common;
using Helperx.DomainClasses.Entities.Common.Enums;
using Helperx.DomainClasses.Entities.Sells;
using Helperx.DomainClasses.Entities.SocialNetworks;
using Helperx.DomainClasses.OtherModels;
using Helperx.ServiceLayer.Contracts;
using Helperx.ServiceLayer.EFServiecs.Common;
using Helperx.ServiceLayer.Inf;
using Helperx.ViewModel.Areas.ControlPanel.Brands;
using Helperx.ViewModel.Areas.ControlPanel.Ports;

namespace Helperx.ServiceLayer.EFServiecs.Sells
{
    public class ConsumptionManager : IConsumptionManager
    {
        private readonly IUnitOfWork _uow;
        //private readonly IDbSet<EntityInstanceServType> _insServTypeRep;
        private readonly IDbSet<ServType> _servTypeRep;
        private readonly IDbSet<ServConsumptionDetailInstance> _consInstanseRep;
        private readonly IDbSet<SellPlanConsumption> _sellPlanConsumptions;
        private readonly IPortService _portService;
        private readonly IMappingEngine _mappingEngine;
        private readonly IDbSet<ServConsumption> _consRep;
        private readonly IDbSet<ServConsumptionDetail> _consDetailRep;
        private readonly IDbSet<Entity> _entityRep;
        private readonly IBrandService _brandService;
        private readonly IDbSet<Port> _portRep;
        private readonly IDbSet<TelegramPort> _tgPortRep;
        private readonly IDbSet<Brand> _brandRep;
        //private readonly IDbSet<ServConsumptionDetailInstance> _servConsDetailInstanseRep;
        private readonly IDbSet<PortType> _portTypeRep;
        private readonly IServTypeService _servTypeService;
        private readonly IServConsumptionService _consService;
        private readonly IServConsumptionDetailService _consDetailService;
        private readonly IDbSet<SocialNetwork> _socialNetworkRep;
        //private readonly IEntityInstanceServTypeService _insServTypeService;

        public ConsumptionManager(
            IUnitOfWork uow
            , IPortService portService
            , IMappingEngine mappingEngine
            , IBrandService brandService
            , IServTypeService servTypeService
            , IServConsumptionService servConsService
        //    , IEntityInstanceServTypeService insServTypeService
            , IServConsumptionDetailService servConsDetailService) //, IMappingEngine mappingEngine, IPortAdminService portAdminService)
        {
            _uow = uow;
            //_insServTypeRep = _uow.Set<EntityInstanceServType>();
            _consInstanseRep = _uow.Set<ServConsumptionDetailInstance>();
            _consRep = _uow.Set<ServConsumption>();
            _consDetailRep = _uow.Set<ServConsumptionDetail>();
            _servTypeRep = _uow.Set<ServType>();
            _portRep = _uow.Set<Port>();
            _brandRep = _uow.Set<Brand>();
            _tgPortRep = _uow.Set<TelegramPort>();
            //_mappingEngine = mappingEngine;
            // _portAdminService = portAdminService;
            _portService = portService;
            _mappingEngine = mappingEngine;
            _brandService = brandService;
            _portTypeRep = _uow.Set<PortType>();
            _servTypeService = servTypeService;
            _consService = servConsService;
        //    _insServTypeService = insServTypeService;
            _consDetailService = servConsDetailService;
            _sellPlanConsumptions = _uow.Set<SellPlanConsumption>();
            _entityRep = _uow.Set<Entity>();
            _socialNetworkRep = _uow.Set<SocialNetwork>();
        }

        public void TryAssignThisPort2AllPossibleServTypes(int portId, long portOwnerUserId)
        {
            // در اینجا با دو نوع-سرویس روبرو هستیم که هر یک نیاز به استراتژی جدا برای چک کردن امکان انتساب و سپس انتساب دارند
            //
            //      A)  نوع-سرویس‌های انتسابی    >>  انتساب به نوع-سرویسی 
            //      B)  نوع-سرویس‌های غیر انتسابی   >>  انتساب به مصرف سرویس 
            //

            //#region#############################  A)  نوع-سرویس‌های انتسابی    >>  انتساب به نوع-سرویسی   ########################################################
            //{
            //    //---------------- بررسی نوع سرویس‌های مجاز----------------

            //    var listOf_ServTypes_And_AmountOfAllowedAssign = (
            //        from sc in _servConsRep
            //        join st in _servTypeRep on sc.ServTypeId equals st.ServTypeId
            //        join scd in _servConsDetailRep on sc.ServConsumptionId equals scd.ServConsumptionId
            //        // برای محاسبه به آن نیاز دارد Used جوین انتیتی ضروروی است چون پراپرتری محاسباتی 
            //        join ent in _entityRep on scd.EntityId equals ent.EntityId

            //        // برای محاسبه به آن نیاز دارد Used لفت جوین اینستنس ضروروی است چون پراپرتری محاسباتی
            //        //from scdei in _servConsDetailEntityInstanceRep.Where(x => x.ServConsumptionDetailId == scd.ServConsumptionDetailId).DefaultIfEmpty()

            //            // Left Join
            //            // انتخاب سرویس‌های فعال و درضمن اگر طرح دارد طرحش هم باید در وضعیت فعال باشد 
            //        from emptySpc in _sellPlanConsumptions.Where(x => x.IsActivated && x.SellPlanConsumptionId == sc.SellPlanConsumptionId).DefaultIfEmpty()

            //            //join spc in _sellPlanConsumptions on sc.SellPlanConsumptionId equals spc.SellPlanConsumptionId into spc1
            //            //from emptySpc in spc1.DefaultIfEmpty()

            //        where
            //            sc.UserId == portOwnerUserId
            //                //  مصرف-سرویس‌هایی که نوع-سرویس‌شان انتسابی است 
            //            && st.IsAssignable == true
            //            && sc.IsActivated
            //                //  فقط آن سرویسهای مصرفی  کاربر که میتوان به آنها پورت منتسب کرد
            //            && scd.EntityId == (int) HxEntities.Port
            //                //  فقط آن سرویس‌های مصرفی که مصرف شدنی هستند و هنوز مانده دارند
            //           // && scd.Remain > 0
            //        select new { st.ServTypeId, scd.Amount}
            //        )
            //        .Decompile()
            //        .ToList();

            //    // حالا میریم ببینیم بابت هر نوع سرویس فعالی که مصرف‌های کاربر نشان میدهد دارد
            //    // در جدول انتساب انتیتی‌ها با نوع سرویس‌ها چندبار به هر نوع سرویس داخل لیست انتساب انجام شده
            //    // اگر مانده داشت آن را برای نوع سروس مربوطه مجاز تلقی میکنیم و انتساب پورت به نوع-سرویس را انجام میدهیم
            //    foreach (var servType_And_AmountOfAllowedAssign in listOf_ServTypes_And_AmountOfAllowedAssign)
            //    {
            //        var curServType = servType_And_AmountOfAllowedAssign.ServTypeId.ConverToEnum<HxServTypes>();
            //        var countOf_AssignedPorts_ToAvalableServType = _insServTypeService.GetAssignedPortsOfUser(portOwnerUserId, curServType).Count();
                    
            //        // اگر تعداد مجاز که از روی مصرف‌ها برای این نوع سرویس بدست آمده
            //        // بزرگتر از تعداد انتساب انجام شده به این نوع سرویس بود
            //        // یعنی اگر باقیمانده داشتیم
            //        if (servType_And_AmountOfAllowedAssign.Amount > countOf_AssignedPorts_ToAvalableServType)
            //        {
            //            //---------------- انتساب به نوع-سرویس‌های مجاز پیدا شده----------------
            //            _insServTypeService.AssignPortToServType(portId, curServType, checkServTypeIsAssignable: false, saveChanges: false);
            //        }
            //    }
                
            //    //---------------- ذخیره سازی یکجای انتسابهای انجام گرفته----------------
            //    _uow.SaveChanges();

            //}
            //#endregion

            #region#############################  B)  نوع-سرویس‌های غیر انتسابی   >>   انتساب به مصرف سرویس    #####################################################
            {
                //________________چک میکنیم ببینیم کدام مصرف سرویس‌ها را داریم که نوع_سرویسشان غیر انتسابی است______________________
                //___________________و درضمن جزئیاتشان حاکی از آن است که امکان انتساب پورت به آنها وجود دارد_______________________
                //_______________________________فرمان انتساب پورت مربوطه را به همان‌ها صادر میکنیم___________________________________

                //---------------- بررسی مصرف سرویسهای مجاز----------------

                var listOf_ServConsDetailsInfo_ThatHasRemain =
                    (
                        from sc in _consRep
                        join st in _servTypeRep on sc.ServTypeId equals st.ServTypeId
                        join scd in _consDetailRep on sc.ServConsumptionId equals scd.ServConsumptionId
                        // برای محاسبه به آن نیاز دارد Used جوین انتیتی ضروروی است چون پراپرتری محاسباتی 
                        join ent in _entityRep on scd.EntityId equals ent.EntityId

                        //Left join
                        // برای محاسبه به آن نیاز دارد Used جوین اینستنس ضروروی است چون پراپرتری محاسباتی 
                        from emptyScdei in _consInstanseRep.Where(x => x.ServConsumptionDetailId == scd.ServConsumptionDetailId).DefaultIfEmpty()
                        // Left Join
                        // انتخاب سرویس‌های فعال و درضمن اگر طرح دارد طرحش هم باید در وضعیت فعال باشد 
                        from emptySpc in _sellPlanConsumptions.Where(x => x.IsActivated && x.SellPlanConsumptionId == sc.SellPlanConsumptionId).DefaultIfEmpty()

                        where
                            sc.UserId == portOwnerUserId
                                //  مصرف-سرویس‌هایی که نوع-سرویس‌شان غیر انتسابی است 
                                //&& st.IsAssignable == false
                            && sc.IsActivated
                            && sc.TermIsCurrent
                                //  فقط آن سرویسهای مصرفی  کاربر که میتوان به آنها پورت منتسب کرد
                            && scd.EntityId == (int) HxEntities.Port
                                //  فقط آن سرویس‌های مصرفی که به سقف تعداد نمونه انتیتی قابل انتساب به آن نرسیده و هنوز جای انتساب دارد
                                // یعنی باقیمانده دارد
                            && scd.Remain > 0//scd.Instances.Count < scd.Amount
                            
                            //TODO نباید این شرط رو اینجا بذاریم چون در مرحله بعد ممکنه لازم باشه موارد قلا دلت شده فعال شودند پس باید در نتایج باشند >>>   && !emptyScdei.IsDeleted
                        select new
                        {
                            Detail = scd
                            //  INCLUDE Entity
                            ,
                            scd.Entity
                            //  INCLUDE Instances
                            ,
                            scd.Instances
                        }
                        )
                        .Decompile()
                        .ToList();

                //---------------- انتساب به جزئیات مصرف سرویس‌های مجاز پیدا شده----------------

                foreach (var servConsumptionDetail_ThatHasRemain in listOf_ServConsDetailsInfo_ThatHasRemain.Select(x=>x.Detail))
                {
                    _consDetailService.AssignEntityInstance(servConsumptionDetail_ThatHasRemain, portId,saveChanges: false);
                }

                //---------------- ذخیره سازی یکجای انتسابهای انجام گرفته----------------
                _uow.SaveChanges();
            }
            #endregion


            //var assignableServTypes = _servTypeService.GetServTypes(justAssignable: true, justHasAmount: false);
            //var list = new List<EntityInstanceServType>();
            //foreach (var servType in assignableServTypes)
            //{
            //    // تعداد مجاز انتساب به سرویس مدنظر
            //    var allowedAssign = all
            //        .Where(x => x.SvType == servType.SvType)
            //        .Select(x =>)
            //        .Sum(x => x.TotalEntityQuantity);
            //    // تعداد منتسب شده‌ها با سرویس مدنظر
            //    var usedAssign = GetCountOfAssignedPortsOfUser(portOwnerUserId, servType.SvType);
            //    if (usedAssign >= allowedAssign)
            //    {
            //        continue;
            //    }
            //    var assignResult = AssignPortToServType(portId, servType.SvType, false);
            //    if (assignResult.Result == HxAssignResult.Done_Assigned || assignResult.Result == HxAssignResult.Done_ThePortHasAlreadyBeenAssigned)
            //    {
            //        list.Add(assignResult.Detail);
            //    }
            //}
            //return list;
        }

        public List<ResultWithDetail<int,ConsumeServiceStatus>> HasPortPermit_ToConsume_ContentSchedulingServ(long portOwnerUserId, List<int> portIds)
        {
            var port_Status_List=new List<ResultWithDetail<int,ConsumeServiceStatus>>();
            var d = new ResultWithDetail<int, ConsumeServiceStatus>();
            var consumeDataResult = GetConsumeStatus_ForPort_ForService(portOwnerUserId, portIds, HxServTypes.Posting);
            foreach (var portId in portIds)
            {
                var consumeDataDetail = consumeDataResult.FirstOrDefault(x => x.PortId == portId)?.Data;
                if (consumeDataDetail == null)
                {
                    throw new Exception("Err:   Consume datat detail not found!");
                }
                if (consumeDataDetail.Status != ConsumeServiceStatus.HasCurrentServiceWithDetail)
                    port_Status_List.Add(d.Fill(portId ,consumeDataDetail.Status));

                var sumRemains = consumeDataDetail.ConsumptionDetail.Where(x => x.EntityEnum == HxEntities.Post).Sum(x => x.Remain);
                port_Status_List.Add(d.Fill(
                    portId, 
                    sumRemains > 0 
                    ? ConsumeServiceStatus.AllowdService 
                    : ConsumeServiceStatus.HasNotRemain));
                
            }
            return port_Status_List;
        }
        public ConsumeServiceStatus HasPortPermit_ToConsume_ContentSchedulingServ(long portOwnerUserId, int portId)
        {
            // ReSharper disable once PossibleNullReferenceException
            return HasPortPermit_ToConsume_ContentSchedulingServ(portOwnerUserId,new List<int>{portId}).FirstOrDefault().Detail;
        }

        /// <summary>
        /// واکشی اطلاعات سرویس‌هایی فعالی که به پورت‌های کاربر متصل هستند 
        /// اگر سرویسی با این شرایط نیابد طبیعتا لیست خالی برگشت میدهد
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="justWhereHasRemain"></param>
        /// <param name="justInCurrentTermCons"></param>
        /// <param name="justInActiveCons"></param>
        /// <param name="justInIndependentCons"></param>
        /// <returns></returns>
        public List<AssinedPortsInfo> ConsInfo_OfAssignedPorts_OfUser(long userId, bool justWhereHasRemain = false, bool justInCurrentTermCons = true, bool justInActiveCons = true, bool justInIndependentCons = false)
        {
            return ConsInfo_OfAssignedPorts_OfUser(userId,null,null, justWhereHasRemain, justInCurrentTermCons, justInActiveCons, justInIndependentCons);
        }

        /// <summary>
        /// واکشی اطلاعات سرویس‌هایی فعالی که به پورت‌های کاربر متصل هستند 
        /// اگر سرویسی با این شرایط نیابد طبیعتا لیست خالی برگشت میدهد
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="portId"></param>
        /// <param name="justWhereHasRemain"></param>
        /// <param name="justInCurrentTermCons"></param>
        /// <param name="justInActiveCons"></param>
        /// <param name="justInIndependentCons"></param>
        /// <returns></returns>
        public List<AssinedPortsInfo> ConsInfo_OfAssignedPort_OfUser(long userId, int portId, bool justWhereHasRemain = false, bool justInCurrentTermCons = true, bool justInActiveCons = true, bool justInIndependentCons = false)
        {
            return ConsInfo_OfAssignedPorts_OfUser(userId,new List<int>() { portId },null, justWhereHasRemain, justInCurrentTermCons, justInActiveCons, justInIndependentCons);
        }

        /// <summary>
        /// واکشی اطلاعات سرویس‌هایی فعالی که به پورت‌های ورودی کاربر متصل هستند 
        /// اگر سرویسی با این شرایط نیابد طبیعتا لیست خالی برگشت میدهد
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="portIds"></param>
        /// <param name="servTypeEnum"></param>
        /// <param name="justWhereHasRemain"></param>
        /// <param name="justInCurrentTermCons"></param>
        /// <param name="justInActiveCons"></param>
        /// <param name="justInIndependentCons"></param>
        /// <param name="justNoneDeletedInstances"></param>
        /// <returns></returns>
        public List<AssinedPortsInfo> ConsInfo_OfAssignedPorts_OfUser(long userId, List<int> portIds, HxServTypes? servTypeEnum=null,bool justWhereHasRemain=false, bool justInCurrentTermCons = true, bool justInActiveCons = true, bool justInIndependentCons = false,bool justNoneDeletedInstances=true)
        {
            var servTypeId = servTypeEnum == null ? (int?) null : (int) servTypeEnum;
            var portIdsIsEmpty = portIds == null || !portIds.Any();
            IQueryable<Port> portQuery;
            if (!portIdsIsEmpty)
            {
                // فقط پورتهای مورد اشاره
                portQuery = (from port in _portRep.Where(x => portIds.Contains(x.PortId)) select port);
            }
            else
            {
                //همه پورتها - البته کوئری زیر کوئری زیرین منظور را متمرکز میکنیم روی همه پورتهای کاربر
                portQuery = (from port in _portRep select port);
            }
            
            // واکشی اطلاعات سرویس‌هایی فعالی که به پورت‌های کاربر متصل هستند 
            // اگر سرویسی با این شرایط نیابد طبیعتا لیست خالی برگشت میدهد
            var query= (
                   from port in portQuery
                   from brand in _brandRep.Where(x => x.OwnerUserId == userId && x.BrandId == port.BrandId)
                   from scdi in _consInstanseRep.Where(x => x.InstanceId == port.PortId && (!x.IsDeleted || !justNoneDeletedInstances) )
                   from scd in _consDetailRep.Where(x => x.ServConsumptionDetailId == scdi.ServConsumptionDetailId && (x.Remain>0 || !justWhereHasRemain))
                   from sc in _consRep.Where(x => x.ServConsumptionId == scd.ServConsumptionId && (x.TermIsCurrent || !justInCurrentTermCons) && (x.IsActivated || !justInActiveCons) && (x.IsIndependent || !justInIndependentCons))
                   from st in _servTypeRep.Where(x => x.ServTypeId == sc.ServTypeId && (servTypeId == null || x.ServTypeId== servTypeId))
                   from entity in _entityRep.Where(x => x.EntityId == scd.EntityId && x.EntityId == (int)HxEntities.Port)
                   select new AssinedPortsInfo
                   {
                       BrandId = brand.BrandId,
                       BrandGuid = brand.BrandGuid,
                       PortId = port.PortId,
                       PortGuid = port.PortGuid,
                       SocialNetworkId = port.SocialNetworkId,
                       PortTypeId = port.PortTypeId,
                    //PortTypeCssClass = portType.CssClass,
                    //Title = port.TitleFa == "" ? port.TitleEn : port.TitleFa,
                    ServType = st,
                       Cons = sc,
                       ConsDetail = scd,
                       ConsInstance = scdi,
                       Entity = entity
                   })
                   .Decompile()
                   .ToList();

            return query;
        }

        public List<PortConsumDataResult> GetConsumeStatus_ForPort_ForService(long portOwnerUserId, List<int> portIds, HxServTypes servType)
        {
            var result = new List<PortConsumDataResult>();
            var data = new PortConsumDataDetail();
            var consInfoOfAssignedPorts = ConsInfo_OfAssignedPorts_OfUser(portOwnerUserId, portIds, servType, justWhereHasRemain: false, justInCurrentTermCons: true, justInActiveCons: true, justInIndependentCons: false);

            foreach (var portId in portIds)
            {
                var consData_OfCurrentPort = consInfoOfAssignedPorts.Where(x => x.PortId == portId).ToList();

                //_________________________________________________________________________________________________________________________________________________________
                // اگر پورت منتسب شده بود ولی رکورد مصرفگری سرویس برای صاحب آن یافت نشده
                // یعنی صاحب پورت این سرویس رو نداره
                if (consData_OfCurrentPort.Any(x => x.Cons == null))
                {
                    result.Add(new PortConsumDataResult(portId, data.Fill(ConsumeServiceStatus.PortOwnerHasNotThisService, null)));
                    continue;
                }
                //_________________________________________________________________________________________________________________________________________________________
                //// اگر پورت مربوطه مصرف سرویس دارد ولی دست کم یکی از آنها جزئیات ندارد
                //if (curListOf_PortConsumeData.Any(x => x.ConsDetail == null))
                //{
                //    // اشکالی در کار است. تمام رکودهای مصرفی باید جزئیات داشته باشند ولی دست کم یکی از آنها ندارد!
                //    // یعنی داده‌های پایگاه ناقص ثبت شده
                //    result.Add(new PortConsumDataResult(portId, data.Fill(ConsumeServiceStatus.ServConsumptionDetailNotFound, null)));
                //}

                //_________________________________________________________________________________________________________________________________________________________
                //اما اگر هم پورت منتسب شده بود به نوع سرویس مربوطه و هم رکورد مصرفگر سرویس برای صاحب آن پورت موجود بود
                // اقدام میکنیم به تعیین وضعیت تمامی رکوردهای مصرفگر سرویس که این برند می‌تواند برطبق آنها مجاز به داشتن سرویس مدنظر کند

                // اگر همه رکوردهای مصرفگری صاحب پورت برای نوع سرویس مربوطه پایان مهلت را نشان میداد
                if (consData_OfCurrentPort.All(x => x.Cons.TermIsCurrent == false))
                {
                    result.Add(new PortConsumDataResult(portId, data.Fill(ConsumeServiceStatus.HasNotAnyTermInCurrent, null)));
                    continue;
                }

                // اگر همه رکوردهای مصرفگری صاحب پورت برای نوع سرویس مربوطه وضعیت غیرفعال داشتند
                if (consData_OfCurrentPort.All(x => x.Cons.IsActivated == false))
                {
                    result.Add(new PortConsumDataResult(portId, data.Fill(ConsumeServiceStatus.HasNotAnyActiveCons, null)));
                    continue;
                }

                // اگر تمام نتایج نشان از اتمام باقیمانده قابل مصرف داشت
                if (consData_OfCurrentPort.All(x => x.ConsDetail.Remain <= 0))
                {
                    result.Add(new PortConsumDataResult(portId, data.Fill(ConsumeServiceStatus.HasNotRemain, null)));
                    continue;
                }

                //_________________________________________________________________________________________________________________________________________________________
                var detailList = consData_OfCurrentPort
                    // شروط حیاتی و مهم
                    .Where(x => x.Cons.TermIsCurrent && x.Cons.IsActivated && x.ConsDetail.Remain>0)
                    .Select(item => item.ConsDetail)
                    .ToList();
                result.Add(new PortConsumDataResult(portId, data.Fill(ConsumeServiceStatus.HasCurrentServiceWithDetail, detailList)));
                continue;
            }
            return result;
        }
        
        public PortConsumDataDetail GetConsumeStatus_ForPort_ForService(long userId, int portId, HxServTypes servType)
        {
            return GetConsumeStatus_ForPort_ForService(userId, new List<int>() {portId}, servType)?.FirstOrDefault()?.Data;
        }


        /// <summary>
        /// پورت‌های یک کاربر برای دریافت برخی سرویس‌ها مثل زمانبندی که سریس‌های از نوع انتسابی هستند باید نوع-سرویس مربوطه منتسب شوند
        /// این متد همه پورت‌های کاربر را لیست میکند و همچنین تعیین میکند که کدام پورت با چه مشخصاتی به کدام سرویس انتسابی متصل شده است یا اصلا انتسابی ندارد
        /// 
        /// منتهی
        /// نتیجه را بر اساس برند گروه‌بندی و سپس بر اساس "پورت - نوع سرویس" دسته بندی میکند و به خروجی میفرستد
        /// چیزی شبیه این:
        /// 
        ///     Brand1
        ///         |
        ///         |__Port1 > AssignToServType 100
        ///         |__Port1 > AssignToServType 105  
        ///         |__Port1 > AssignToServCons 20 + بقیه اطلاعات مثل میزان مانده مصرف  
        ///         |__Port2 > AssignToServType 100
        ///     Brand2
        ///         |
        ///         |__Port3 > AssignToServType 102
        ///         |__Port4 > AssignToServType 100
        ///         |__Port4 > AssignToServType 105
        ///         |__Port1 > AssignToServCons 46 + بقیه اطلاعات مثل میزان مانده مصرف  
        /// 
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<ShowAssignedPortsOfBrandToServTypeViewModel> GetAssignInfo_OfUserPorts_GroupedByBrands(long userId)
        {
            // واکشی اطلاعات سرویس‌هایی فعالی که به پورت‌های کاربر متصل هستند 
            // اگر سرویسی با این شرایط نیابد طبیعتا لیست خالی برگشت میدهد
            var consInfo_OfAssignedPorts = ConsInfo_OfAssignedPorts_OfUser(userId, justWhereHasRemain: false, justInActiveCons: true, justInCurrentTermCons: true, justInIndependentCons: false);
            
            // را که در دستورات بالا واکشی کردیم جوین بزنیم با پورتهای کاربر AssinedPortsInfo چون میخواهیم یک لیست از آبجکتهای 
            // پشتیبانی نمی‌شود لذا linqToEntity و این کار در
            // لیست پورتها را هم میکشیم در حافظه تا در حافظه لینک بزنیم نه روی پایگاه
            // اینطوری کوئری لینکمون کار میکنه
            var portList = _portService.GetAllPortsByUserId(userId, includeItsSocialPort: true);

            // حالا لیست پورتهای کاربر را با نتایج کوئری بالا لفت جوین میکنیم
            var assignedPortInfo_ToServCons = (
                from port in portList
                // چون کوئری قبلی ممکن است نال برگرداند در اینجا دوباره جوین میزنیم
                from portType in _portTypeRep.Where(x => x.PortTypeId == port.PortTypeId)
                // چون کوئری قبلی ممکن است نال برگرداند در اینجا دوباره جوین میزنیم
                from brand in _brandRep.Where(x => x.OwnerUserId == userId && x.BrandId == port.BrandId)
                // لفت جوین با کوئری قبلی
                from emptyleft in consInfo_OfAssignedPorts.Where(x => x.PortId == port.PortId).DefaultIfEmpty()
                select new AssinedPortsInfo
                {
                    BrandId = brand.BrandId,
                    BrandGuid = brand.BrandGuid,
                    PortId = port.PortId,
                    PortGuid = port.PortGuid,
                    SocialNetworkId = port.SocialNetworkId,
                    PortTypeId = portType.PortTypeId,
                    PortTypeCssClass = portType.CssClass,
                    Title = port.TitleFa == "" ? port.TitleEn : port.TitleFa,
                    ServType = emptyleft?.ServType,
                    Cons = emptyleft?.Cons,
                    ConsDetail = emptyleft?.ConsDetail,
                    ConsInstance = emptyleft?.ConsInstance,
                    Entity = emptyleft?.Entity
                })
                .ToList();

            //################################################################################################################################
            // حالا دو نوع نتیجه بالا را با هم یکی میکنیم و کیفرستیم برای الگوریتم دسته بندی

            var fullPortInfo = new List<AssinedPortsInfo>();
            //  fullPortInfo.AddRange(assignedPortInfo_ToServTypes);
            fullPortInfo.AddRange(assignedPortInfo_ToServCons);

            // الگوریتم گروه‌بندی و دسته بندی نتایج
            //     Brand1
            //         |
            //         |__Port1 > AssignToServType 100
            //         |__Port1 > AssignToServType 105  
            //         |__Port1 > AssignToServCons 20 + بقیه اطلاعات مثل میزان مانده مصرف  
            //         |__Port2 > AssignToServType 100
            //     Brand2
            //         |
            //         |__Port3 > AssignToServType 102
            //         |__Port4 > AssignToServType 100
            //         |__Port4 > AssignToServType 105
            //         |__Port1 > AssignToServCons 46 + بقیه اطلاعات مثل میزان مانده مصرف  

            var final = new List<ShowAssignedPortsOfBrandToServTypeViewModel>();
            var curBrandId = 0;
            var curPortId = 0;
            var sortedBrandAndPortInfo = fullPortInfo.OrderBy(x => x.BrandId).ThenBy(x => x.PortId).ToList();
            var searchedBrands = new List<int>();
            foreach (var item in fullPortInfo)
            {
                if (searchedBrands.Contains(item.BrandId))
                    continue;
                var portLevel = new ShowAssignedPortToServTypeViewModel();
                var brandLevel = new ShowAssignedPortsOfBrandToServTypeViewModel();
                if (curBrandId != item.BrandId)
                {
                    curBrandId = item.BrandId;
                    brandLevel = new ShowAssignedPortsOfBrandToServTypeViewModel
                    {
                        BrandId = item.BrandId,
                        BrandGuid = item.BrandGuid,
                        BrandTitle = item.Title
                    };
                    final.Add(brandLevel);
                }
                foreach (var innerItem in sortedBrandAndPortInfo.Where(x => x.BrandId == item.BrandId))
                {
                    if (curPortId != innerItem.PortId)
                    {
                        curPortId = innerItem.PortId;
                        portLevel = new ShowAssignedPortToServTypeViewModel()
                        {
                            PortId = innerItem.PortId,
                            PortGuid = innerItem.PortGuid,
                            PortTitle = innerItem.Title,
                            SocialNetworkId = innerItem.SocialNetworkId,
                            PortTypeCssClass = innerItem.PortTypeCssClass,
                            PortTypeId = innerItem.PortTypeId,
                        };
                        brandLevel.AssignPortToServTypeInfo.Add(portLevel);
                    }

                    if (innerItem.ServType != null)
                    {
                        if (!innerItem.ConsInstance?.IsDeleted ?? false)
                        {
                            portLevel.AssignedServTypes.Add(innerItem.ServType.ServTypeId);
                        }
                    }
                    if (innerItem.ConsInstance != null)
                    {
                        //    // به یک دلیلی که آن را نفهمیدم موقع تهیه کوئری بالا از انتیتی فریمورک
                        //    // و ساخت: assignedPortInfo_ToServCons
                        //    // مقدار مدل مصرف در مدل دیتیل نال می‌افتد
                        //    // ولی به صورت مجزا آن را در نتایج همان کوئری در فیلد دیگری داریم
                        //    // لذا خودمان به طور دستی فیلد مصرف را در دیتیل با مقداری که داریم پر میکنیم
                        //    // تا با خطای نال بودن در ویو مواجه نشویم
                        //    if (innerItem.EmptyServConsDetailInstance.ServConsumptionDetail.ServConsumption==null)
                        //    {
                        //        innerItem.EmptyServConsDetailInstance.ServConsumptionDetail.ServConsumption = innerItem.Empty_ServCons;
                        //    }
                        if (!innerItem.ConsInstance?.IsDeleted ?? false)
                        {
                            portLevel.AssignedServCons.Add(innerItem.ConsInstance);
                        }
                    }

                    searchedBrands.Add(innerItem.BrandId);
                }
            }
            return final;
        }

        public IQueryable<Port> GetPortsOfUser_ThatCanConsum(long userId, HxServTypes servTypeEnum, HxEntities entityEnum,bool justInCurrentTermCons=true, bool justInActiveCons=true,bool justInIndependentCons=false)
        {
            // اطمینان از انتسابی نبودن نوع-سرویس ورودی
            SureServTypeIsUnAssinable(servTypeEnum);

            var result = (
                from p in _portRep
                from brand in _brandRep.Where(x => x.OwnerUserId == userId && x.BrandId==p.BrandId)
                //join tgPort in _tgPortRep on p.PortId equals tgPort.Port.PortId
                from cons in _consRep.Where(x=>x.UserId== brand.OwnerUserId &&  (x.TermIsCurrent || !justInCurrentTermCons) && (x.IsActivated || !justInActiveCons) && (x.IsIndependent || !justInIndependentCons))
                from st in _servTypeRep.Where(x => x.ServTypeId == cons.ServTypeId && x.ServTypeId == (int)servTypeEnum)
                join consDetail in _consDetailRep on cons.ServConsumptionId equals consDetail.ServConsumptionId
                from ins in _consInstanseRep.Where(x => x.ServConsumptionDetailId == consDetail.ServConsumptionDetailId && !x.IsDeleted)
                from ent in _entityRep.Where(x => x.EntityId == (int)entityEnum && x.EntityId == consDetail.EntityId)
                select p)
                .Decompile()
                .Distinct();

            return result;
        }

        private void SureServTypeIsUnAssinable(HxServTypes servType)
        {
            if (_servTypeService.ServTypeIsAssinable(servType))
            {
                throw new Exception("ServType is Assignable");
            }
        }

        /// <summary>
        /// حذف انتصاب یک پورت مشخص از تمام مصرف‌سرویس‌هایی که نوعشان در ورودی این قید شده
        /// 
        /// مثلا حذف انتساب پورت نیکمگ از تمام مصرف‌سرویس‌های جاری که نوع سرویسشان زمانبندی است
        /// </summary>
        /// <param name="svType"></param>
        /// <param name="entityEnum"></param>
        /// <param name="consumerInstanceId"></param>
        /// <param name="consumerUserId"></param>
        /// <returns></returns>
        public void UnAssign(HxServTypes svType, HxEntities entityEnum, int consumerInstanceId, long consumerUserId)
        {
            var assinedItems = _consDetailRep
                .Include(x => x.Instances)
                .Include(x => x.ServConsumption)
                .Where(x =>

                    // شرط مهم اینه که حتما رکورد مصرفی باید جاری باشه
                    // چون ما نمیخواییم سوابق ارتباطات پورت ورودی را به مصرف‌سرویس‌های قدیمی از بین ببریم
                    // اونها حکم آرشیو را دارند برای ما
                    x.ServConsumption.TermIsCurrent
                    // حتما باید جستجو در نوع‌سرویس ورودی انجام شود
                    && x.ServConsumption.ServTypeId==(int)svType
                    && x.EntityId == (int) entityEnum
                    && x.Instances.Any()
                    && x.Instances.Select(y => y.InstanceId).Contains(consumerInstanceId)
                    && x.ServConsumption.UserId == consumerUserId)
                .Decompile()
                .ToList();
            if (!assinedItems.Any())
            {
                return;
            }
            // قطع انتساب پورت وروودی از تمام مصرف‌سرویس‌های جاری که به آنها وصل است
            foreach (var assignedItem in assinedItems)
            {
                _consDetailService.UnAssignEntityInstance(assignedItem, consumerInstanceId, saveChanges: false);
            }
            _uow.SaveChanges();
        }

        public void UnAssignBasedOnMaxAllowedAssignments(long consumerUserId)
        {
            throw new NotImplementedException();
        }

        public bool ChangeAmount(Guid servConsDetailGuid, int newTotal)
        {
            return _consDetailService.ChangeAmount(servConsDetailGuid, newTotal);
        }

        public bool IsAssigned(HxServTypes servType, HxEntities entityEnum, int consumerInstanceId, long? consumerUserId=null,bool justSearchInActiveCons=true,bool justSearchInCurrentCons=true)
        {
            return _consInstanseRep
                .Include(x => x.ServConsumptionDetail)
                .Include(x => x.ServConsumptionDetail.ServConsumption)
                .Decompile()
                .Any(x =>
                    x.InstanceId == consumerInstanceId
                    // جستجو فقط در انتساب‌های حذف‌نشده یا به عبارتی انتساب‌های فعال
                    && x.IsDeleted==false
                    && x.ServConsumptionDetail.EntityId == (int) entityEnum
                    && x.ServConsumptionDetail.ServConsumption.ServTypeId == (int) servType
                    && (x.ServConsumptionDetail.ServConsumption.UserId == consumerUserId || consumerUserId == null)
                    && (x.ServConsumptionDetail.ServConsumption.IsActivated || !justSearchInActiveCons)
                    && (x.ServConsumptionDetail.ServConsumption.TermIsCurrent || !justSearchInCurrentCons)
                );
        }

        public Task<bool> Assign(HxServTypes servType, HxEntities entityEnum, int consumerInstanceId, long consumerUserId)
        {
            // این شرط مهمی است چون دستورات لینک زیر -پراپرتی‌های فرمولی باقیمانده و مصرف شده سرویس- روی این شرط در اینجا حساب باز کرده‌اند
            if (DomainClasses.Inf.Inf.EntityIsConsumable((int) entityEnum))
            {
                // مثلا:    نمیشود یک پست را به سرویس مصرفی منتسب کرد ولی میشود یک درگاه را به یک سرویس مصرفی وصل نمود  
                throw new Exception("خطا:   انتیتی ورودی از نوع مصرفی است ولی این متد برای انتیتی‌های غیرمصرفی طراحی شده است");
            }

            // نمونه انتیتی‌ها مثلا یک پورت حق ندارد بیش از یکبار به رکوردهای مصرف-سرویس فعال و جاری منتسب شود
            // لذا اگر قبلا به یک مورد فعال جاری منتسب شده جلوی انتساب‌های دیگر آن نمونه انتیتی را به رکوردهای مصرف‌-سرویسِ در دسترس میگیریم
            // اگر قبلا منتسب شده
            if (IsAssigned(servType, entityEnum, consumerInstanceId, consumerUserId))
            {
                // خروج با پاسخ مثبت
                return Task.FromResult(true);
            }

            //throw new NotImplementedException();
            //if (_servTypeService.ServTypeIsAssinable(servType))
            //{
            //    var result=_insServTypeService.AssignPortToServType(consumerInstanceId, servType, checkServTypeIsAssignable: false, saveChanges: true);
            //    if (result.Result == HxAssignResult.Done_Assigned)
            //    {
            //        return Task.FromResult(true);
            //    }
            //    else
            //    {
            //        return Task.FromResult(false);
            //    }
            //}
            //else
            {
                //_____________________________________________________________________________________________________________________
                // یافتن یک جزئیات-سرویس-مصرفی مناسب 
                // در بین موارد فعال و جاری
                // برای نوع-سرویس  و انتیتی ورودی
                // برای انتساب نمونه-انتیتی ورودی به لیست انتیتی‌های آن

                var servConsDetail = (
                    from sc in _consRep
                    //join st in _servTypeRep on sc.ServTypeId equals st.ServTypeId
                    join scd in _consDetailRep on sc.ServConsumptionId equals scd.ServConsumptionId
                    join st in _servTypeRep on sc.ServTypeId equals st.ServTypeId
                    // برای محاسبه به آن نیاز دارد Used جوین انتیتی ضروروی است چون پراپرتری محاسباتی 
                    join ent in _entityRep on scd.EntityId equals ent.EntityId
                    // Left Join
                    // برای محاسبه به آن نیاز دارد Used جوین اینستنس ضروروی است چون پراپرتری محاسباتی 
                    join servConsDetailEntityInstance in _consInstanseRep on
                        scd.ServConsumptionDetailId equals servConsDetailEntityInstance.ServConsumptionDetailId into scdei1
                    from emptyScdei in scdei1.DefaultIfEmpty()

                    // Left Join
                    //join spc in _sellPlanConsumptions on sc.SellPlanConsumptionId equals spc.SellPlanConsumptionId into spc1
                    //from emptySpc in spc1.DefaultIfEmpty()

                    where
                        st.ServTypeId == (int) servType
                        &&
                        // جستجو بین سرویس مصرفی‌های فعال
                        sc.IsActivated
                        &&
                        // جستجو بین سرویس مصرفی‌های جاری
                        sc.TermIsCurrent
                        &&
                        // انتخاب سرویس‌های مصرفی که صاحب آن کاربر ورودی است
                        sc.UserId == consumerUserId
                        &&
                        // انتخاب جزئیاتی که شناسه انتیتی آن‌ها با ورودی میخواند
                        scd.EntityId == (int) entityEnum
                        //  فقط آن سرویس‌های مصرفی که مانده قابل مصرف دارند
                        &&
                        // جایی که باقیمانده داره
                        scd.Remain > 0
                    select scd
                    )
                    .Decompile()
                    .FirstOrDefault();

                if (servConsDetail != null)
                {
                    _consDetailService.AssignEntityInstance(servConsDetail.ServConsumptionDetailId, consumerInstanceId, saveChanges: true);
                    // خروج با پاسخ مثبت
                    return Task.FromResult(true);
                }
                else
                {
                    // خروج با پاسخ منفی
                    return Task.FromResult(false);
                }
            }
        }

        public Task<bool> Consume(List<long> consumerInstanceIds, ConsumeTypes consumeTypes,HxServTypes servType,HxEntities entity,  long consumerUserId)
        {
            try
            {
                // این شرط مهمی است چون دستورات لینک زیر -پراپرتی‌های فرمولی باقیمانده و مصرف شده سرویس- روی این شرط در اینجا حساب باز کرده‌اند
                if (!DomainClasses.Inf.Inf.EntityIsConsumable((int)entity))
                {
                    throw new Exception("خطا:   انتیتی ورودی از نوع مصرفی نیست ولی این متد برای انتیتی‌های مصرف شدنی طراحی شده است");
                }

                //_____________________________________________________________________________________________________________________
                // اول تمام سرویس‌های مصرفی جاری هم نوع با نوع سرویس ورودی را واکشی میکنیم

                var listOf_ServConsDetails = (
                    from sc in _consRep
                    //join st in _servTypeRep on sc.ServTypeId equals st.ServTypeId
                    join scd in _consDetailRep on sc.ServConsumptionId equals scd.ServConsumptionId
                    // برای محاسبه به آن نیاز دارد Used جوین انتیتی ضروروی است چون پراپرتری محاسباتی 
                    join ent in _entityRep on scd.EntityId equals ent.EntityId
                    join scdei in _consInstanseRep on scd.ServConsumptionDetailId equals scdei.ServConsumptionDetailId into scdei1
                    from emptyScdei in scdei1.DefaultIfEmpty()
                    // Left Join
                    // انتخاب سرویس‌های فعال و درضمن اگر طرح دارد طرحش هم باید در وضعیت فعال باشد 
                    //        from emptySpc in _sellPlanConsumptions.Where(x => x.IsActivated && x.SellPlanConsumptionId == sc.SellPlanConsumptionId).DefaultIfEmpty()
                    //join spc in _sellPlanConsumptions on sc.SellPlanConsumptionId equals spc.SellPlanConsumptionId into spc1
                    //from emptySpc in spc1.DefaultIfEmpty()

                    where
                        // انتخاب سرویس‌های مصرفی که صاحب آن کاربر ورودی است
                        sc.UserId == consumerUserId
                        // فقط سرویس‌های فعال
                        && sc.IsActivated
                        // فقط سرویس‌های جاری
                        && sc.TermIsCurrent
                        // انتخاب جزئیاتی که شناسه انتیتی آن‌ها با ورودی میخواند
                        && scd.EntityId == (int) entity
                        //  فقط آن سرویس‌های مصرفی که مانده قابل مصرف دارند
                        && scd.Remain>0
                      //  && !scd.Instances.Any() || scd.Amount > scd.Instances.Sum(x => x.Used)
                    select new
                    {
                        IsIndependent = sc.SellPlanConsumptionId == null
                       , ServConsDetail = scd
                        ,scd.Instances
                        ,ent
                    })
                    .Decompile()
                    .ToList();
                
                
                //_____________________________________________________________________________________________________________________
                // نتایج واکشی شده را 2 دسته میکنیم: سرویس‌های مستقل جاری   و   سرویس‌های جاری طرح جاری و آنها را به ترتیب صعودی مانده مرتب میکنیم
                // تا آنهایی که کمترین مانده را دارند بالای صف قرار بگیرند. میخواهیم با این کار کاری کنیم که همیشه آن سرویسی که کمترین میزان را دارد زودتر تمام شود
                // و اگر مصرف منفی داریم باز هم به آنهایی که میزان کمتری دارند اضافه شود
                // چراکه اینطوری درنظر مشتری در جاهایی مثل صفحه داشبورد، یک سرویس مصرف نشدهٔ پر، در اثر تعاملات مصرفی پیشین مقدارش زیاد نمیشود

                var indConsDetails = listOf_ServConsDetails
                    .Where(x => x.IsIndependent)
                    .Select(x => x.ServConsDetail)
                    .OrderBy(x => x.Remain)
                    .ToList();

                var spConsDetail = listOf_ServConsDetails
                    .Where(x => !x.IsIndependent)
                    .Select(x => x.ServConsDetail)
                    .OrderBy(x => x.Remain)
                    .ToList();
                //_____________________________________________________________________________________________________________________
                // حالا در نتایج سرویس‌های مستقل حرکت میکنیم و از آن‌ها مصرف میکنیم

                foreach (var insId in consumerInstanceIds)
                {
                    var consumeUnits = 1;
                    var signedConsumeUnits = consumeUnits * (consumeTypes == ConsumeTypes.Refund ? -1 : 1);
                    var remainToSave = consumeUnits;

                    foreach (var detail in indConsDetails)
                    {
                        var detailRemain = detail.Remain;
                        if (detailRemain > remainToSave)
                        {
                            remainToSave = remainToSave - detailRemain;
                            _consDetailService.ChangeUse(detail, insId, signedConsumeUnits, saveChanges: false);
                            break;
                        }
                        else
                        {
                            remainToSave = remainToSave - detailRemain;
                            _consDetailService.ChangeUse(detail, insId, detailRemain, saveChanges: false);
                        }
                    }
                    //_____________________________________________________________________________________________________________________
                    // اگر به انتهای سرویس‌های مستقل رسیدیم و هنوز مصرف ثبت نشده داشتیم
                    // وارد سرویس-مصرفی‌های طرح جاری میشویم و از آنها مصرف میکنیم
                    if (remainToSave > 0)
                    {
                        foreach (var detail in spConsDetail)
                        {
                            var detailRemain = detail.Remain;
                            if (detailRemain > remainToSave)
                            {
                                remainToSave = remainToSave - detailRemain;
                                _consDetailService.ChangeUse(detail, insId, signedConsumeUnits, saveChanges: false);
                                break;
                            }
                            else
                            {
                                remainToSave = remainToSave - detailRemain;
                                _consDetailService.ChangeUse(detail, insId, detailRemain, saveChanges: false);
                            }
                        }
                    }
                   
                    {
                        // مطمئن میشویم که همه مصرف‌های انجام گرفته را ثبت کرده ایم
                        if (remainToSave > 0)
                        {
                            //TODO
                        }
                    }
                }
                // ذخیره سازی
                _uow.SaveChanges();

                return Task.FromResult<bool>(true);
            }

            catch (Exception)
            {
                return Task.FromResult<bool>(false);
            }
        }
        

        public async Task<bool> ConsumePostPlanningServ(List<long> postIds, ConsumeTypes consumeTypes, long portOwnerUserId)
        {
            return await Consume(postIds, consumeTypes, HxServTypes.Posting, HxEntities.Post, portOwnerUserId);
        }

        public ServTerms GetBalanceOf_PostPlanningServ(long userId, bool includeExpieredItems = false)
        {
            return _consService.GetBalanceOf_PostingServType(userId, includeExpieredItems);
        }

        public ServTerms GetBalanceOf_EntityOf_Servonsumption(long userId, HxServTypes servType, HxEntities entity, bool includeExpieredItems = false)
        {
            return _consService.GetBalanceOf_EntityOf_Servonsumption(userId,servType,entity ,includeExpieredItems);
        }

        public List<int> GetBrandIds_ThatAtlastOneOfItsPorts_HasActiveCurrentAssignPort_ToTheServTyp_InConsuptionse(List<int> brandIds, HxServTypes servType)
        {
            var result = (
                from p in _portRep
                join brand in _brandRep on p.BrandId equals brand.BrandId
                join ins in _consInstanseRep on p.PortId equals ins.InstanceId
                join detail in _consDetailRep on ins.ServConsumptionDetailId equals detail.ServConsumptionDetailId
                join cons in _consRep on detail.ServConsumptionId equals cons.ServConsumptionId
                where (
                    // فقط سرویس-مصرفی‌های فعال و جاری
                    cons.IsActivated
                    && cons.TermIsCurrent
                    && cons.ServTypeId==(int)servType
                    && detail.EntityId == (int)HxEntities.Port
                    // فقط اتصال‌های حذف نشده
                    && !ins.IsDeleted
                    && brandIds.Contains(brand.BrandId)
                    )
                select brand.BrandId
                )
                .Decompile()
                .ToList();

            return result;
            //return  _insServTypeRep
            //    .Include(x => x.Port)
            //    .Include(x => x.Port.Brand)
            //    .Select(x => new {x.ServTypeId, x.Port, x.Port.Brand.BrandId})
            //    .Where(x => brandIds.Contains(x.BrandId) && x.ServTypeId == (int) servType)
            //    .Select(x => x.BrandId)
            //    .ToList();
        }

        //public List<SocialNetwork> GetSocialNetworksOfBrand_ThatHasPostRemainInThem(int brandId, HxServTypes servType)
        //{
        //    var result = (
        //            from p in _portRep
        //            join sn in _socialNetworkRep on p.SocialNetworkId equals sn.SocialNetworkId
        //            join brand in _brandRep on p.BrandId equals brand.BrandId
        //            join cons in _consRep on brand.OwnerUserId equals cons.UserId
        //            from detail in _consDetailRep.Where(x => x.ServConsumptionId == cons.ServConsumptionId)
        //            where (
        //                cons.IsActivated
        //                && cons.TermIsCurrent
        //                && cons.ServTypeId == (int) servType
        //                && detail.EntityId == (int) HxEntities.Post
        //                && brand.BrandId == brandId
        //                // جایی که سرویس-مصرفی مربوطه باقیمانده قابل مصرف دارد
        //                && detail.Remain > 0
        //            )
        //            select sn
        //        )
        //        .Decompile()
        //        .ToList();

        //    return result;
        //}

        //
        public List<SocialNetwork> GetSocialNetworks_OfPortsOfBrand_ThaHave_ActiveCurrentPortAssing_ToTheServTyp_ToConsumption(int brandId, HxServTypes servType)
        {
            return GetPortsOfBrand_ThaHave_ActiveCurrentPortAssing_ToTheServTyp_ToConsumption(brandId, servType).Select(x => x.SocialNetwork).ToList();
        }

        public IEnumerable<Port> GetPortsOfBrand_ThaHave_ActiveCurrentPortAssing_ToTheServTyp_ToConsumption(int brandId, HxServTypes servType)
        {
            var query = (
                    from p in _portRep
                    join sn in _socialNetworkRep on p.SocialNetworkId equals sn.SocialNetworkId
                    join brand in _brandRep on p.BrandId equals brand.BrandId
                    join ins in _consInstanseRep on p.PortId equals ins.InstanceId
                    join detail in _consDetailRep on ins.ServConsumptionDetailId equals detail.ServConsumptionDetailId
                    join cons in _consRep on detail.ServConsumptionId equals cons.ServConsumptionId

                    where (
                        cons.IsActivated
                        && cons.TermIsCurrent
                        && cons.ServTypeId == (int) servType
                        && detail.EntityId == (int) HxEntities.Port
                        && brand.BrandId == brandId
                        // فقط اتصال‌های حذف نشده
                        && !ins.IsDeleted
                    )
                    select new {p,sn}
                )
                .Decompile()
                //.Include();
                //.ToList()
                ;

            var res= query.ToList();
            return res.Select(x=>x.p);
        }

        public void CalcAndUpdateRemain(int consDetailId,bool saveChanges=true)
        {
            var detail = _consDetailService.Get(consDetailId, includeInstances: true);
            if (detail == null)
            {
                return;
            }
            CalcAndUpdateRemain(detail);
        }
        public void CalcAndUpdateRemain(ServConsumptionDetail consDetail_Inc_Instances, bool saveChanges = true)
        {
            if (consDetail_Inc_Instances.Instances == null)
            {
                throw new Exception("باید نمونه‌ها در جزئیات اینکلود شده باشند");
            }
          
            var calcedUsed= consDetail_Inc_Instances.Instances.Count(x => !x.IsDeleted);
            if (consDetail_Inc_Instances.Used == calcedUsed)
            {
                return;
            }
            consDetail_Inc_Instances.Used = calcedUsed;
            _uow.MarkAsChanged(consDetail_Inc_Instances);
            if (saveChanges)
            {
                _uow.SaveChanges();
            }
        }


        public void CalcAndUpdateRemain(ServConsumption cons_Inc_DetailAndInstances, bool saveChanges = true)
        {
            if (cons_Inc_DetailAndInstances == null)
            {
                return;
            }
            if (cons_Inc_DetailAndInstances.ServConsumptionDetails == null)
            {
                throw new Exception("باید جزئیات در مصرف اینکلود شده باشد");
            }
            foreach (var consDetail_Inc_Instances in cons_Inc_DetailAndInstances.ServConsumptionDetails)
            {
                CalcAndUpdateRemain(consDetail_Inc_Instances,saveChanges:false);
            }
            if (saveChanges)
            {
                _uow.SaveChanges();
            }
        }
    }










    public class ConsumeData
    {
        private int? _portId;
        public ServConsumptionDetailInstance Empty_Instance { get; set; }

        public ServConsumption ServCons { get; set; }

        public ServConsumptionDetail ServConsDetail { get; set; }

        public int? PortId
        {
            get { return _portId ??(int?) Empty_Instance?.InstanceId; }
            set { _portId = value; }
        }
    }


    public class PortConsumDataResult
    {
        public PortConsumDataResult()
        {

        }

        public PortConsumDataResult(int portId, PortConsumDataDetail data)
        {
            Data = data;
            PortId = portId;
        }

        public PortConsumDataResult Fill(int portId, PortConsumDataDetail data)
        {
            Data = data;
            PortId = portId;
            return this;
        }

        public int PortId { get; private set; }
        public PortConsumDataDetail Data { get; private set; }
    }

    public class PortConsumDataDetail
    {
        public PortConsumDataDetail()
        {

        }

        public PortConsumDataDetail(ConsumeServiceStatus status, List<ServConsumptionDetail> consumptionDetail)
        {
            ConsumptionDetail = consumptionDetail;
            Status = status;
        }

        public PortConsumDataDetail Fill(ConsumeServiceStatus status, List<ServConsumptionDetail> consumption)
        {
            ConsumptionDetail = consumption;
            Status = status;
            return this;
        }

        public ConsumeServiceStatus Status { get; private set; }
        public List<ServConsumptionDetail> ConsumptionDetail { get; private set; }
    }
}