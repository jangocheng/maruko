﻿using System;
using System.Linq;
using log4net;
using log4net.Core;
using Maruko.Domain.Entities;
using Maruko.Domain.UnitOfWork;
using Maruko.EntityFrameworkCore.Context;
using Maruko.Logger;
using Microsoft.EntityFrameworkCore;

namespace Maruko.EntityFrameworkCore.UnitOfWork
{
    /// <summary>
    /// ef core 的工作单元具体实现
    /// </summary>
    public class EfCoreBaseUnitOfWork : IDataBaseUnitOfWork
    {
        public DefaultDbContext DefaultDbContext;

        public ILog Logger { get; }

        public EfCoreBaseUnitOfWork()
        {
            Logger = LogHelper.Log4NetInstance.LogFactory(typeof(EfCoreBaseUnitOfWork));
        }

        public void Dispose()
        {
            if (DefaultDbContext != null)
                DefaultDbContext.Dispose();
        }

        public void Commit()
        {
            try
            {
                //这里如果没有调用过createset方法就会报错，如果没有调用认为没有任何改变直接跳出来
                if (DefaultDbContext == null)
                    return;
                DefaultDbContext.SaveChanges();
            }
            catch (DbUpdateException ex)
            {
                Logger.Debug("DbUpdateException:" + ex.Message);
                throw new Exception(ex.Message);
            }
        }

        public void CommitAndRefreshChanges()
        {
            bool saveFailed;
            do
            {
                try
                {
                    DefaultDbContext.SaveChanges();
                    saveFailed = false;
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    saveFailed = true;
                    ex.Entries.ToList()
                        .ForEach(entry => entry.OriginalValues.SetValues(entry.GetDatabaseValues()));
                }
                catch (DbUpdateException ex)
                {
                    Logger.Debug("DbUpdateException:" + ex.Message);
                    throw new Exception(ex.Message);
                }
            } while (saveFailed);
        }

        public void RollbackChanges()
        {
            // set all entities in change tracker
            // as 'unchanged state'
            if (DefaultDbContext != null)
                DefaultDbContext.ChangeTracker.Entries()
                    .ToList()
                    .ForEach(entry =>
                    {
                        if (entry.State != EntityState.Unchanged)
                            entry.State = EntityState.Detached;
                    });


        }

        public DbSet<TEntity> CreateSet<TEntity>(ContextType contextType) where TEntity : class, IEntity
        {
            if (contextType == ContextType.DefaultContextType)
            {
                DefaultDbContext = new DefaultDbContext();
                return DefaultDbContext.CreateSet<TEntity>();
            }
            else
            {
                Logger.Debug("不存在这样的ContextType");
                return null;
            }
        }


        public void SetModify<TEntity>(TEntity entity) where TEntity : class, IEntity
        {
            if (DefaultDbContext != null)
                DefaultDbContext.Entry(entity).State = EntityState.Modified;
        }


        #region Isql
        public int ExecuteCommand(string sqlCommand, ContextType contextType = ContextType.DefaultContextType, params object[] parameters)
        {
            switch (contextType)
            {
                default:
                    return GeneralDbContext(sqlCommand, parameters);
            }
        }

        private int GeneralDbContext(string sqlCommand, params object[] parameters)
        {
            if (DefaultDbContext != null)
                return DefaultDbContext.Database.ExecuteSqlCommand(sqlCommand, parameters);
            Logger.Debug("DefaultDbContext 创建失败");
            return -1;
        }

        #endregion


    }
}