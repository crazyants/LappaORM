﻿// Copyright (C) Arctium Software.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Lappa_ORM.Misc;

namespace Lappa_ORM
{
    public partial class Database
    {
        public bool Create<TEntity>(MySqlEngine dbEngine = MySqlEngine.MyISAM, bool replaceTable = false) where TEntity : Entity, new()
        {
            // Only MySql supported for now.
            if (connSettings.ConnectionType != ConnectionType.MySql)
                return false;

            // Check if table exists or is allowed to be replaced.
            if (!Exists<TEntity>() || replaceTable)
            {
                // Exclude foreign key and non db related properties.
                var properties = typeof(TEntity).GetReadWriteProperties();
                var fields = new Dictionary<string, PropertyInfo>();
                var query = new QueryBuilder<TEntity>(querySettings, properties);
                var entity = new TEntity();

                // Key: GroupStartIndex, Value: GroupCount
                var groups = new ConcurrentDictionary<int, int>();
                var lastGroupName = "";
                var lastGroupStartIndex = 0;

                // Get Groups
                for (var i = 0; i < properties.Length; i++)
                {
                    var group = properties[i].GetCustomAttribute<GroupAttribute>();

                    if (group != null)
                    {
                        if (group.Name == lastGroupName)
                        {
                            groups[lastGroupStartIndex] += 1;
                        }
                        else
                        {
                            lastGroupName = group.Name;
                            lastGroupStartIndex = i;

                            groups.TryAdd(lastGroupStartIndex, 1);
                        }
                    }
                }

                for (var i = 0; i < properties.Length; i++)
                {
                    var groupCount = 0;

                    if (!properties[i].PropertyType.IsArray)
                        fields.Add(properties[i].Name, properties[i]);
                    else
                    {
                        if (groups.TryGetValue(i, out groupCount))
                        {
                            var arr = properties[i].GetValue(Activator.CreateInstance(typeof(TEntity))) as Array;

                            for (var k = 1; k <= arr.Length; k++)
                            {
                                for (var j = 0; j < groupCount; j++)
                                {
                                    fields.Add(properties[i + j].Name + k, properties[i + j]);
                                }
                            }

                            i += groupCount - 1;
                        }
                        else
                        {
                            var arr = (query.PropertyGetter[i].GetValue(entity) as Array);

                            for (var j = 1; j <= arr.Length; j++)
                                fields.Add(properties[i].Name + j, properties[i]);
                        }
                    }
                }

                return Execute(query.BuildTableCreate(fields, dbEngine));
            }

            return false;
        }
    }
}
