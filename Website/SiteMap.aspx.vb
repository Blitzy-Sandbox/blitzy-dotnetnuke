'
' DotNetNuke® - http://www.dotnetnuke.com
' Copyright (c) 2002-2008
' by DotNetNuke Corporation
'
' Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
' documentation files (the "Software"), to deal in the Software without restriction, including without limitation 
' the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and 
' to permit persons to whom the Software is furnished to do so, subject to the following conditions:
'
' The above copyright notice and this permission notice shall be included in all copies or substantial portions 
' of the Software.
'
' THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED 
' TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL 
' THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF 
' CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
' DEALINGS IN THE SOFTWARE.
'
Imports System.Text
Imports DotNetNuke.Entities.Tabs

Namespace DotNetNuke.Common.Utilities

    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The LinkClick Page processes links
    ''' </summary>
    ''' <returns></returns>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Partial Class SiteMap
        Inherits Framework.PageBase

        Const SITEMAP_CHANGEFREQ As String = "daily"
        Const SITEMAP_PRIORITY As String = "0.8"
        Const SITEMAP_MAXURLS As Integer = 50000

#Region "Event Handlers"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Page_Load runs when the control is loaded.
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub Page_Load(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.Load

            Try
                Response.ContentType = "text/xml"
                Response.ContentEncoding = Encoding.UTF8
                Response.Write(BuildSiteMap(PortalSettings.PortalId))
            Catch exc As Exception

            End Try

        End Sub

#End Region

#Region "Private Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Builds SiteMap
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks>
        ''' </remarks>
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function BuildSiteMap(ByVal PortalID As Integer) As String

            Dim sb As New StringBuilder(1024)
            Dim URL As String

            ' build header
            sb.Append("<?xml version=""1.0"" encoding=""UTF-8""?>" & ControlChars.CrLf)
            sb.Append("<urlset xmlns=""http://www.sitemaps.org/schemas/sitemap/0.9"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xsi:schemaLocation=""http://www.sitemaps.org/schemas/sitemap/0.9 http://www.sitemaps.org/schemas/sitemap/0.9/sitemap.xsd"">" & ControlChars.CrLf)

            ' add urls
            Dim intURLs As Integer = 0
            Dim objTabs As New TabController
            For Each objTab As TabInfo In objTabs.GetTabs(PortalID)
                If objTab.IsDeleted = False AndAlso objTab.DisableLink = False AndAlso objTab.IsVisible AndAlso objTab.TabType = TabType.Normal AndAlso ((Null.IsNull(objTab.StartDate) = True OrElse objTab.StartDate < Now) AndAlso (Null.IsNull(objTab.EndDate) = True OrElse objTab.EndDate > Now)) Then
                    ' the crawler is an anonymous user therefore the site map will only contain publicly accessible pages
                    If PortalSecurity.IsInRoles(objTab.AuthorizedRoles) Then
                        If intURLs < SITEMAP_MAXURLS Then
                            intURLs += 1
                            URL = objTab.FullUrl
                            If URL.ToLower.IndexOf(Request.Url.Host.ToLower) = -1 Then
                                URL = AddHTTP(Request.Url.Host) & URL
                            End If
                            sb.Append(BuildURL(URL, 2))
                        End If
                    End If
                End If
            Next

            sb.Append("</urlset>")

            Return sb.ToString

        End Function

        Private Function BuildURL(ByVal URL As String, ByVal Indent As Integer) As String

            Dim sb As New StringBuilder(1024)

            sb.Append(WriteElement("url", Indent))
            sb.Append(WriteElement("loc", URL, Indent + 1))
            sb.Append(WriteElement("lastmod", DateTime.Now.ToString("yyyy-MM-dd"), Indent + 1))
            sb.Append(WriteElement("changefreq", SITEMAP_CHANGEFREQ, Indent + 1))
            sb.Append(WriteElement("priority", SITEMAP_PRIORITY, Indent + 1))
            sb.Append(WriteElement("/url", Indent))

            Return sb.ToString

        End Function

        Private Function WriteElement(ByVal Element As String, ByVal Indent As Integer) As String
            Dim InputLength As Integer = Element.Trim.Length + 20
            Dim sb As New StringBuilder(InputLength)
            sb.Append(ControlChars.CrLf.PadRight(Indent + 2, ControlChars.Tab))
            sb.Append("<").Append(Element).Append(">")
            Return sb.ToString
        End Function

        Private Function WriteElement(ByVal Element As String, ByVal ElementValue As String, ByVal Indent As Integer) As String
            Dim InputLength As Integer = Element.Trim.Length + ElementValue.Trim.Length + 20
            Dim sb As New StringBuilder(InputLength)
            sb.Append(ControlChars.CrLf.PadRight(Indent + 2, ControlChars.Tab))
            sb.Append("<").Append(Element).Append(">")
            sb.Append(ElementValue)
            sb.Append("</").Append(Element).Append(">")
            Return sb.ToString
        End Function

#End Region

    End Class

End Namespace
