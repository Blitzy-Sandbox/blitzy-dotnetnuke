Imports System
Imports System.Web
Imports DotNetNuke.Entities.Modules
Imports DotNetNuke.Services.Search

Namespace DotNetNuke.Services.Syndication
    Public Class RssHandler : Inherits SyndicationHandlerBase

        ''' <summary>
        ''' This method
        ''' </summary>
        ''' <param name="channelName"></param>
        ''' <param name="userName"></param>
        ''' <remarks></remarks>
        Protected Overrides Sub PopulateChannel(ByVal channelName As String, ByVal userName As String)
            Dim objModules As New ModuleController
            Dim objModule As ModuleInfo

            Channel("title") = settings.PortalName
            Channel("link") = AddHTTP(GetDomainName(Request))
            If settings.Description <> "" Then
                Channel("description") = settings.Description
            Else
                Channel("description") = settings.PortalName
            End If
            Channel("language") = settings.DefaultLanguage
            Channel("copyright") = settings.FooterText
            Channel("webMaster") = settings.Email

            For Each objResult As SearchResultsInfo In SearchDataStoreProvider.Instance.GetSearchItems(Settings.PortalId, TabId, ModuleId)
                If PortalSecurity.IsInRoles(Settings.ActiveTab.AuthorizedRoles) Then
                    If Settings.ActiveTab.StartDate < Now And Settings.ActiveTab.EndDate > Now Then
                        objModule = objModules.GetModule(objResult.ModuleId, objResult.TabId)
                        If objModule.DisplaySyndicate = True And objModule.IsDeleted = False Then
                            If PortalSecurity.IsInRoles(objModule.AuthorizedViewRoles) = True Then
                                If CType(IIf(objModule.StartDate = Null.NullDate, Date.MinValue, objModule.StartDate), Date) < Now And CType(IIf(objModule.EndDate = Null.NullDate, Date.MaxValue, objModule.EndDate), Date) > Now Then
                                    Channel.Items.Add(GetRssItem(objResult))
                                End If
                            End If
                        End If
                    End If
                End If
            Next
        End Sub

        ''' <summary>
        ''' Creates an RSS Item
        ''' </summary>
        ''' <param name="SearchItem"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Private Function GetRssItem(ByVal SearchItem As SearchResultsInfo) As GenericRssElement
            Dim item As GenericRssElement = New GenericRssElement()
            Dim URL As String = NavigateURL(SearchItem.TabId)
            If URL.ToLower.IndexOf(HttpContext.Current.Request.Url.Host.ToLower) = -1 Then
                URL = AddHTTP(HttpContext.Current.Request.Url.Host) & URL
            End If

            item("title") = SearchItem.Title
            item("description") = SearchItem.Description
            item("link") = URL
            'TODO:  JMB: We need to figure out how to persist the dc prefix in the XML output.  See the Render method below.
            'item("dc:creator") = SearchItem.AuthorName
            item("pubDate") = SearchItem.PubDate.ToUniversalTime.ToString("r")

            item("guid") = URL
            If Not String.IsNullOrEmpty(SearchItem.Guid) Then
                If URL.Contains("?") Then
                    item("guid") += "&" & SearchItem.Guid
                Else
                    item("guid") += "?" & SearchItem.Guid
                End If
            End If
            Return item
        End Function

        ''' <summary>
        ''' The PreRender event is used to set the Caching Policy for the Feed.  This mimics the behavior from the 
        ''' OutputCache directive in the old Rss.aspx file.  @OutputCache Duration="60" VaryByParam="moduleid" 
        ''' </summary>
        ''' <param name="source"></param>
        ''' <param name="e"></param>
        ''' <remarks></remarks>
        Private Sub RssHandler_PreRender(ByVal source As Object, ByVal e As System.EventArgs) Handles Me.PreRender
            Context.Response.Cache.SetExpires(DateTime.Now.AddSeconds(60))
            Context.Response.Cache.SetCacheability(HttpCacheability.Public)
            Context.Response.Cache.VaryByParams("moduleid") = True
        End Sub

    End Class
End Namespace