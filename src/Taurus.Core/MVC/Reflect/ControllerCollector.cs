﻿using CYQ.Data;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.IO;
using Taurus.Plugin.Doc;
using Taurus.Plugin.Admin;
using Taurus.Plugin.MicroService;

namespace Taurus.Mvc.Reflect
{
    /// <summary>
    /// Controller 类搜索器
    /// </summary>
    public static class ControllerCollector
    {



        #region GetControllers
        /// <summary>
        /// 存档一级名称的控制器[Controller]
        /// </summary>
        private static Dictionary<string, TypeEntity> _Lv1Controllers = new Dictionary<string, TypeEntity>(StringComparer.OrdinalIgnoreCase);
        /// <summary>
        /// 存档二级名称的控制器[Module.Controller]
        /// </summary>
        private static Dictionary<string, TypeEntity> _Lv2Controllers = new Dictionary<string, TypeEntity>(StringComparer.OrdinalIgnoreCase);
        private static readonly object objLock = new object();
        internal static bool InitControllers()
        {
            if (_Lv1Controllers.Count == 0)
            {
                lock (objLock)
                {
                    if (_Lv1Controllers.Count == 0)
                    {
                        List<Assembly> assList = AssemblyCollector.ControllerAssemblyList;
                        if (assList != null && assList.Count > 0)
                        {
                            for (int i = 0; i < assList.Count; i++)
                            {
                                Assembly ass = assList[i];
                                bool isControllerAssembly = false;
                                Type[] typeList = ass.GetExportedTypes();
                                foreach (Type type in typeList)
                                {

                                    //三层继承判断，应该够用了。
                                    if (type.BaseType != null && (type.BaseType.FullName == ReflectConst.TaurusMvcController
                                        || (type.BaseType.BaseType != null && (type.BaseType.BaseType.FullName == ReflectConst.TaurusMvcController
                                        || (type.BaseType.BaseType.BaseType != null && (type.BaseType.BaseType.BaseType.FullName == ReflectConst.TaurusMvcController
                                        || (type.BaseType.BaseType.BaseType.BaseType != null && type.BaseType.BaseType.BaseType.BaseType.FullName == ReflectConst.TaurusMvcController)
                                        ))
                                         ))
                                         ))
                                    {
                                        isControllerAssembly = true;
                                        TypeEntity entity = null;
                                        string lv1Name = GetLevelName(type.FullName, 1);
                                        string lv2Name = GetLevelName(type.FullName, 2);
                                        if (!_Lv1Controllers.ContainsKey(lv1Name))
                                        {
                                            entity = new TypeEntity(type);
                                            _Lv1Controllers.Add(lv1Name, entity);
                                        }
                                        else
                                        {
                                            int value = string.Compare(lv2Name, GetLevelName(_Lv1Controllers[lv1Name].Type.FullName, 2), true);
                                            if (value == -1)
                                            {
                                                entity = new TypeEntity(type);
                                                _Lv1Controllers[lv1Name] = entity;//值小的优化。
                                            }
                                        }
                                        if (!_Lv2Controllers.ContainsKey(lv2Name))
                                        {
                                            entity = new TypeEntity(type);
                                            _Lv2Controllers.Add(lv2Name, entity);
                                        }
                                        MethodCollector.InitMethodInfo(type);
                                    }

                                }
                                if (!isControllerAssembly)
                                {
                                    assList.RemoveAt(i);
                                    i--;
                                }
                            }
                        }

                        #region Admin（优化，内部有初始化配置功能）、Doc、 插件
                        Type adminType = typeof(AdminController);
                        TypeEntity adminEntity = new TypeEntity(adminType);
                        string path = AdminConfig.Path.Trim('/', '\\');
                        if (!_Lv1Controllers.ContainsKey(path))
                        {
                            _Lv1Controllers.Add(path, adminEntity);//用path，允许调整路径
                        }
                        MethodCollector.InitMethodInfo(adminType);

                        Type docType = typeof(DocController);
                        TypeEntity docEntity = new TypeEntity(docType);
                        path = DocConfig.Path.Trim('/', '\\');
                        if (!_Lv1Controllers.ContainsKey(path))
                        {
                            _Lv1Controllers.Add(path, docEntity);
                        }
                        MethodCollector.InitMethodInfo(docType);


                        #endregion

                        Type msType = typeof(MicroServiceController);
                        TypeEntity msEntity = new TypeEntity(msType);
                        path = MsConfig.Server.RcPath.Trim('/', '\\');
                        //微服务API
                        if (!_Lv1Controllers.ContainsKey(path))
                        {
                            _Lv1Controllers.Add(path, msEntity);
                        }

                        path = MsConfig.Client.RcPath.Trim('/', '\\');
                        //微服务API
                        if (!_Lv1Controllers.ContainsKey(path))
                        {
                            _Lv1Controllers.Add(path, msEntity);
                        }

                        MethodCollector.InitMethodInfo(msType);
                    }
                }
            }
            return _Lv1Controllers.Count > 0;
        }
        /// <summary>
        /// 获取所有Mvc控制器
        /// <param name="level">1、以ControllerName为key；2、以NameSpace.ControllerName为Key</param>
        /// </summary>
        public static Dictionary<string, TypeEntity> GetControllers(int level)
        {
            InitControllers();
            return level == 1 ? _Lv1Controllers : _Lv2Controllers;
        }
        /// <summary>
        /// 存档N级名称（Module.Controller)
        /// </summary>
        /// <param name="fullName"></param>
        /// <returns></returns>
        private static string GetLevelName(string fullName, int level)
        {
            string[] items = fullName.Split('.');
            string lv1Name = items[items.Length - 1].Replace(ReflectConst.Controller, "");
            if (level == 2)
            {
                return items[items.Length - 2] + "." + lv1Name;
            }
            return lv1Name;
        }
        /// <summary>
        /// 通过className类名获得对应的Controller类
        /// </summary>
        /// <returns></returns>
        public static TypeEntity GetController(string className)
        {
            if (string.IsNullOrEmpty(className) || !IsModuleEnable(className))
            {
                className = ReflectConst.Global;
            }
            Dictionary<string, TypeEntity> controllers = GetControllers(1);
            string[] names = className.Split('.');//home/index
            if (MvcConfig.RouteMode == 1 || names.Length == 1)
            {
                if (controllers.ContainsKey(names[0]))
                {
                    return controllers[names[0]];
                }
                if (names.Length > 1 && controllers.ContainsKey(names[1]))
                {
                    return controllers[names[1]];
                }
            }
            else if (MvcConfig.RouteMode == 2)
            {
                Dictionary<string, TypeEntity> controllers2 = GetControllers(2);
                if (controllers2.ContainsKey(className))
                {
                    return controllers2[className];
                }
                //再查一级路径
                if (controllers.ContainsKey(names[1]))
                {
                    return controllers[names[1]];
                }
                //兼容【路由1=》（变更为）2】
                if (controllers.ContainsKey(names[0]))
                {
                    return controllers[names[0]];
                }
            }

            if (controllers.ContainsKey(ReflectConst.Global))
            {
                return controllers[ReflectConst.Global];
            }
            return null;
        }

        internal static bool IsModuleEnable(string name)
        {
            if (!AdminConfig.IsEnable && name == AdminConfig.Path.Trim('/', '\\'))
            {
                return false;
            }
            if (!DocConfig.IsEnable && name == DocConfig.Path.Trim('/', '\\'))
            {
                return false;
            }

            return true;
        }

        #endregion

        #region 修改控制器路径

        /// <summary>
        /// 修改控制器请求映射路径
        /// </summary>
        /// <returns></returns>
        public static bool ChangePath(string oldPath, string newPath)
        {
            if (string.IsNullOrEmpty(oldPath) || string.IsNullOrEmpty(newPath))
            {
                return false;
            }
            oldPath = oldPath.Trim('/', '\\');
            newPath = newPath.Trim('/', '\\');
            if (oldPath != newPath && _Lv1Controllers.ContainsKey(oldPath) && !_Lv1Controllers.ContainsKey(newPath))
            {
                _Lv1Controllers.Add(newPath, _Lv1Controllers[oldPath]);
                _Lv1Controllers.Remove(oldPath);
                return true;
            }
            return false;
        }

        #endregion
    }
}
