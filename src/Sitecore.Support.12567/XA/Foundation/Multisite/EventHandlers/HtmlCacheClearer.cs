namespace Sitecore.Support.XA.Foundation.Multisite.EventHandlers
{
  using Microsoft.Extensions.DependencyInjection;
  using Sitecore;
  using Sitecore.Buckets.Extensions;
  using Sitecore.Caching;
  using Sitecore.Configuration;
  using Sitecore.Data;
  using Sitecore.Data.Events;
  using Sitecore.Data.Items;
  using Sitecore.DependencyInjection;
  using Sitecore.Diagnostics;
  using Sitecore.Events;
  using Sitecore.Links;
  using Sitecore.Publishing;
  using Sitecore.Sites;
  using Sitecore.Web;
  using Sitecore.XA.Foundation.Multisite;
  using Sitecore.XA.Foundation.Multisite.Extensions;
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Xml;

  public class HtmlCacheClearer : Sitecore.XA.Foundation.Multisite.EventHandlers.HtmlCacheClearer
  {
    private readonly IEnumerable<ID> _fieldIds;
    public HtmlCacheClearer() : base()
    {
      IEnumerable<XmlNode> source = Factory.GetConfigNodes("experienceAccelerator/multisite/htmlCacheClearer/fieldID").Cast<XmlNode>();
      _fieldIds = from node in source
                  select new ID(node.InnerText);
    }

    public new void OnPublishEnd(object sender, EventArgs args)
    {
      Assert.ArgumentNotNull(sender, "sender");
      Assert.ArgumentNotNull(args, "args");
      SitecoreEventArgs sitecoreEventArgs = args as SitecoreEventArgs;
      if (sitecoreEventArgs != null)
      {
        Publisher publisher = sitecoreEventArgs.Parameters[0] as Publisher;
        #region Modified code
        if (publisher != null)
        {
          if (publisher.Options.RootItem != null)
          {
            List<SiteInfo> sitesToClear = GetUsages(publisher.Options.RootItem);
            if (sitesToClear.Count > 0)
            {
              sitesToClear.ForEach(ClearSiteCache);
              return;
            }
          }
        }
      }
      base.ClearCache(sender, args);
      ClearAllSxaSitesCaches();
      #endregion
    }

    public new void OnPublishEndRemote(object sender, EventArgs args)
    {
      Assert.ArgumentNotNull(sender, "sender");
      Assert.ArgumentNotNull(args, "args");
      PublishEndRemoteEventArgs publishEndRemoteEventArgs = args as PublishEndRemoteEventArgs;
      #region Modified code
      if (publishEndRemoteEventArgs != null)
      {
        Database database = Factory.GetDatabase(publishEndRemoteEventArgs.TargetDatabaseName, false);
        Item rootItem = database?.GetItem(new ID(publishEndRemoteEventArgs.RootItemId));
        if (rootItem != null)
        {
          List<SiteInfo> sitesToClear = GetUsages(rootItem);
          if (sitesToClear.Count > 0)
          {
            sitesToClear.ForEach(ClearSiteCache);
            return;
          }
        }
      }
      base.ClearCache(sender, args);
      ClearAllSxaSitesCaches();
      #endregion
    }
    #region Added code
    protected virtual void ClearAllSxaSitesCaches()
    {
      foreach (Site item in from site in SiteManager.GetSites()
                            where site.IsSxaSite()
                            select site)
      {
        ClearSiteCache(item.Name);
      }
    }

    private void ClearSiteCache(string siteName)
    {
      Log.Info(String.Format("HtmlCacheClearer clearing cache for {0} site", siteName), this);
      ProcessSite(siteName);
      Log.Info("HtmlCacheClearer done.", this);
    }
    #endregion
    private void ClearSiteCache(SiteInfo site)
    {
      Log.Info($"HtmlCacheClearer clearing cache for {site.Name} site", this);
      ProcessSite(site.Name);
      Log.Info("HtmlCacheClearer done.", this);
    }

    private void ProcessSite(string siteName)
    {
      SiteContext site = Factory.GetSite(siteName);
      if (site != null)
      {
        HtmlCache htmlCache = CacheManager.GetHtmlCache(site);
        htmlCache?.Clear();
      }
    }

    private List<SiteInfo> GetUsages(Item item)
    {
      Assert.IsNotNull(item, "item");
      List<SiteInfo> list = new List<SiteInfo>();
      Item item2 = item;
      do
      {
        if (MultisiteContext.GetSiteItem(item2) != null)
        {
          SiteInfo siteInfo = SiteInfoResolver.GetSiteInfo(item2);
          if (siteInfo != null)
          {
            list.Add(siteInfo);
            break;
          }
        }
        ItemLink[] itemReferrers = Globals.LinkDatabase.GetItemReferrers(item2, false);
        foreach (ItemLink itemLink in itemReferrers)
        {
          if (IsOneOfWanted(itemLink.SourceFieldID))
          {
            Item sourceItem = itemLink.GetSourceItem();
            SiteInfo siteInfo2 = SiteInfoResolver.GetSiteInfo(sourceItem);
            list.Add(siteInfo2);
          }
        }
        item2 = item2.Parent;
      }
      while (item2 != null);
      list = (from s in list
              where s != null
              select s into g
              group g by new
              {
                g.Name
              } into x
              select x.First()).ToList();
      list.AddRange(GetAllSitesForSharedSites(list));
      return list;
    }
    private bool IsOneOfWanted(ID sourceFieldId)
    {
      return _fieldIds.Any((ID x) => x.Equals(sourceFieldId));
    }
  }
}