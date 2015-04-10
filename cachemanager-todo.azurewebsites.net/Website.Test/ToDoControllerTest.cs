﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using CacheManager.Core;
using Microsoft.Practices.Unity;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Website.Controllers;
using System.Diagnostics;

namespace Website.Test
{
    [TestClass]
    public class ToDoControllerTest
    {
        private static IUnityContainer container;

        [ClassInitialize()]
        public static void ClassInit(TestContext context)
        {
            container = new UnityContainer();

            var cache = CacheFactory.Build("todos", settings =>
            {
                settings
                    .WithSystemRuntimeCacheHandle("inprocess")
                    .And
                    .WithRedisConfiguration("redisLocal", "localhost:6379,ssl=false,allowAdmin=true")
                    .WithRedisBackPlate("redisLocal")
                    .WithRedisCacheHandle("redisLocal", true);

            });

            container.RegisterInstance(cache);
            container.RegisterInstance(cache);
        }

        [TestMethod]
        public void ToDoController_ManyNewItems_Threaded()
        {
            var controller = container.Resolve<ToDoController>();
            var cache = container.Resolve<ICacheManager<object>>();
            var threads = 10;
            var iterations = 10;

            cache.Clear();

            var random = new Random();

            Run(() =>
            {
                controller.Post(new Models.Todo()
                {
                    Completed = false,
                    Title = "New title" + random.Next()
                });
            }, threads, iterations);

            var items = controller.Get().ToArray();
            var allKeys = cache.Get<long[]>("todo-sample-keys");
            var uniqueKeys = allKeys.Distinct().ToArray();

            Assert.IsTrue(items.Length == threads * iterations, "should be threads * iterations");
            Assert.IsTrue(allKeys.Length == threads * iterations);
            Assert.IsTrue(uniqueKeys.Length == threads * iterations);

            cache.Clear();
        }

        public static void Run(Action test, int threads, int iterations)
        {
            var threadList = new List<Thread>();

            Exception exResult = null;
            for (int i = 0; i < threads; i++)
            {
                var t = new Thread(new ThreadStart(() =>
                {
                    for (var iter = 0; iter < iterations; iter++)
                    {
                        try
                        {
                            test();
                        }
                        catch (Exception ex)
                        {
                            exResult = ex;
                        }
                    }
                }));
                threadList.Add(t);
            }

            threadList.ForEach(p => p.Start());
            threadList.ForEach(p => p.Join());

            if (exResult != null)
            {
                Trace.TraceError(exResult.Message + "\n\r" + exResult.StackTrace);
                throw exResult;
            }
        }
    }
}
