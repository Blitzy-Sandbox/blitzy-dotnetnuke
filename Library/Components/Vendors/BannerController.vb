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
Imports System
Imports System.Configuration
Imports System.Data

Namespace DotNetNuke.Services.Vendors

    Public Class BannerController

        Public Function GetBanners(ByVal VendorId As Integer) As ArrayList
            Return CBO.FillCollection(DataProvider.Instance().GetBanners(VendorId), GetType(BannerInfo))
        End Function

        Public Function GetBanner(ByVal BannerId As Integer, ByVal VendorId As Integer, ByVal PortalId As Integer) As BannerInfo
            Return CType(CBO.FillObject(DataProvider.Instance().GetBanner(BannerId, VendorId, PortalId), GetType(BannerInfo)), BannerInfo)
        End Function

        Public Function GetBannerGroups(ByVal PortalId As Integer) As DataTable
            Return DataProvider.Instance().GetBannerGroups(PortalId)
        End Function

        Public Sub DeleteBanner(ByVal BannerId As Integer)
            DataProvider.Instance().DeleteBanner(BannerId)
            ClearBannerCache()
        End Sub

        Public Sub AddBanner(ByVal objBannerInfo As BannerInfo)
            DataProvider.Instance().AddBanner(objBannerInfo.BannerName, objBannerInfo.VendorId, objBannerInfo.ImageFile, objBannerInfo.URL, objBannerInfo.Impressions, objBannerInfo.CPM, objBannerInfo.StartDate, objBannerInfo.EndDate, objBannerInfo.CreatedByUser, objBannerInfo.BannerTypeId, objBannerInfo.Description, objBannerInfo.GroupName, objBannerInfo.Criteria, objBannerInfo.Width, objBannerInfo.Height)
            ClearBannerCache()
        End Sub

        Public Sub UpdateBanner(ByVal objBannerInfo As BannerInfo)
            DataProvider.Instance().UpdateBanner(objBannerInfo.BannerId, objBannerInfo.BannerName, objBannerInfo.ImageFile, objBannerInfo.URL, objBannerInfo.Impressions, objBannerInfo.CPM, objBannerInfo.StartDate, objBannerInfo.EndDate, objBannerInfo.CreatedByUser, objBannerInfo.BannerTypeId, objBannerInfo.Description, objBannerInfo.GroupName, objBannerInfo.Criteria, objBannerInfo.Width, objBannerInfo.Height)
            ClearBannerCache()
        End Sub

        Public Sub ClearBannerCache()
            Dim objDictionaryEntry As DictionaryEntry
            For Each objDictionaryEntry In HttpRuntime.Cache
                If CType(objDictionaryEntry.Key, String).StartsWith("Banners:") Then
                    DataCache.RemoveCache(CType(objDictionaryEntry.Key, String))
                End If
            Next
        End Sub

        Public Function LoadBanners(ByVal PortalId As Integer, ByVal ModuleId As Integer, ByVal BannerTypeId As Integer, ByVal GroupName As String, ByVal Banners As Integer) As ArrayList
            Dim arrBanners As ArrayList
            Dim objBanner As BannerInfo
            Dim intCounter As Integer

            If GroupName Is Nothing Then
                GroupName = Null.NullString
            End If

            ' cache key
            Dim strCacheKey As String = "Banners:" & PortalId.ToString & ":" & BannerTypeId.ToString & ":" & GroupName.ToString & ":"

            ' get banners
            arrBanners = CType(DataCache.GetCache(strCacheKey & "ArrayList"), ArrayList)
            If arrBanners Is Nothing Then
                arrBanners = CBO.FillCollection(DataProvider.Instance().FindBanners(PortalId, BannerTypeId, GroupName), GetType(BannerInfo))
                DataCache.SetCache(strCacheKey & "ArrayList", arrBanners)
            End If

            ' create return collection
            Dim arReturnBanners As New ArrayList(Banners)

            If arrBanners.Count > 0 Then
                If Banners > arrBanners.Count Then
                    Banners = arrBanners.Count
                End If

                ' get last index for rotation
                Dim intLastBannerIndex As Integer = 0
                Dim objLastBannerIndex As Object = DataCache.GetCache(strCacheKey & "LastIndex")
                If Not objLastBannerIndex Is Nothing Then
                    intLastBannerIndex = Convert.ToInt32(objLastBannerIndex)
                Else
                    Dim RandomGenerator As New Random()
                    intLastBannerIndex = RandomGenerator.Next(0, arrBanners.Count - 1)
                End If

                intCounter = 1

                While intCounter <= arrBanners.Count And arReturnBanners.Count <> Banners
                    ' manage the rotation 
                    intLastBannerIndex += 1
                    If intLastBannerIndex > (arrBanners.Count - 1) Then
                        intLastBannerIndex = 0
                    End If

                    ' get the banner object
                    objBanner = CType(arrBanners(intLastBannerIndex), BannerInfo)

                    ' add to return collection
                    If IsBannerActive(objBanner) Then
                        arReturnBanners.Add(objBanner)

                        ' update banner ( these values are persisted to the cache )
                        objBanner.Views += 1
                        If Null.IsNull(objBanner.StartDate) Then
                            objBanner.StartDate = Now
                        End If
                        If Null.IsNull(objBanner.EndDate) And objBanner.Views >= objBanner.Impressions And objBanner.Impressions <> 0 Then
                            objBanner.EndDate = Now
                        End If
                        ' update database
                        DataProvider.Instance().UpdateBannerViews(objBanner.BannerId, objBanner.StartDate, objBanner.EndDate)
                    End If

                    intCounter += 1
                End While

                ' save last index for rotation
                DataCache.SetCache(strCacheKey & "LastIndex", intLastBannerIndex, True)
            End If
            Return arReturnBanners

        End Function

        Public Sub UpdateBannerClickThrough(ByVal BannerId As Integer, ByVal VendorId As Integer)
            DataProvider.Instance().UpdateBannerClickThrough(BannerId, VendorId)
        End Sub

        Public Function FormatBanner(ByVal VendorId As Integer, ByVal BannerId As Integer, ByVal BannerTypeId As Integer, ByVal BannerName As String, ByVal ImageFile As String, ByVal Description As String, ByVal URL As String, ByVal Width As Integer, ByVal Height As Integer, ByVal BannerSource As String, ByVal HomeDirectory As String) As String
            Dim strBanner As String = ""

            Dim strWindow As String = "_new"
            If Common.Globals.GetURLType(URL) = TabType.Tab Then
                strWindow = "_self"
            End If

            Dim strURL As String = ""
            If BannerId <> -1 Then
                strURL = Common.Globals.ApplicationPath & "/Admin/Vendors/BannerClickThrough.aspx?BannerId=" & BannerId.ToString & "&VendorId=" & VendorId.ToString & "&PortalId=" & GetPortalSettings.PortalId
            Else
                strURL = URL
            End If

            Select Case BannerTypeId
                Case BannerType.Text
                    strBanner += "<a href=""" & strURL & """ class=""NormalBold"" target=""" & strWindow & """ rel=""nofollow""><u>" & BannerName & "</u></a><br>"
                    strBanner += "<span class=""Normal"">" & Description & "</span><br>"
                    If ImageFile <> "" Then
                        URL = ImageFile
                    End If
                    If URL.IndexOf("://") <> -1 Then
                        URL = URL.Substring(URL.IndexOf("://") + 3)
                    End If
                    strBanner += "<a href=""" & strURL & """ class=""NormalRed"" target=""" & strWindow & """ rel=""nofollow"">" & URL & "</a>"
                Case BannerType.Script
                    strBanner += Description
                Case Else
                    If ImageFile.IndexOf("://") = -1 And ImageFile.StartsWith("/") = False Then
                        If ImageFile.ToLowerInvariant.IndexOf(".swf") = -1 Then
                            strBanner += "<a href=""" & strURL & """ target=""" & strWindow & """ rel=""nofollow"">"
                            Select Case BannerSource
                                Case "L"       ' local
                                    strBanner += FormatImage(HomeDirectory & ImageFile, Width, Height, BannerName, Description)
                                Case "G"       ' global
                                    strBanner += FormatImage(Common.Globals.HostPath & ImageFile, Width, Height, BannerName, Description)
                            End Select
                            strBanner += "</a>"
                        Else ' flash
                            Select Case BannerSource
                                Case "L"       ' local
                                    strBanner += FormatFlash(HomeDirectory & ImageFile, Width, Height)
                                Case "G"       ' global
                                    strBanner += FormatFlash(Common.Globals.HostPath & ImageFile, Width, Height)
                            End Select
                        End If
                    Else
                        If ImageFile.ToLowerInvariant.IndexOf(".swf") = -1 Then
                            strBanner += "<a href=""" & strURL & """ target=""" & strWindow & """ rel=""nofollow"">"
                            strBanner += FormatImage(ImageFile, Width, Height, BannerName, Description)
                            strBanner += "</a>"
                        Else ' flash
                            strBanner += FormatFlash(ImageFile, Width, Height)
                        End If
                    End If
            End Select

            Return strBanner
        End Function

        Private Function FormatImage(ByVal File As String, ByVal Width As Integer, ByVal Height As Integer, ByVal BannerName As String, ByVal Description As String) As String

            Dim Image As New Text.StringBuilder()

            Image.Append("<img src=""" & File & """ border=""0""")
            If Description <> "" Then
                Image.Append(" alt=""" & Description & """")
            Else
                Image.Append(" alt=""" & BannerName & """")
            End If
            If Width > 0 Then
                Image.Append(" width=""" & Width.ToString & """")
            End If
            If Height > 0 Then
                Image.Append(" height=""" & Height.ToString & """")
            End If
            Image.Append("/>")

            Return Image.ToString()

        End Function

        Private Function FormatFlash(ByVal File As String, ByVal Width As Integer, ByVal Height As Integer) As String

            Dim Flash As String = ""

            Flash += "<object classid=""clsid:D27CDB6E-AE6D-11cf-96B8-444553540000"" codebase=""http://download.macromedia.com/pub/shockwave/cabs/flash/swflash.cab#version=4,0,2,0"" width=""" & Width.ToString & """ height=""" & Height.ToString & """>"
            Flash += "<param name=movie value=""" & File & """>"
            Flash += "<param name=quality value=high>"
            Flash += "<embed src=""" & File & """ quality=high pluginspage=""http://www.macromedia.com/shockwave/download/index.cgi?P1_Prod_Version=ShockwaveFlash"" type=""application/x-shockwave-flash"" width=""" & Width.ToString & """ height=""" & Height.ToString & """>"
            Flash += "</embed>"
            Flash += "</object>"

            Return Flash

        End Function

        Public Function IsBannerActive(ByVal objBanner As BannerInfo) As Boolean
            Dim blnValid As Boolean = True

            If Null.IsNull(objBanner.StartDate) = False And Now < objBanner.StartDate Then
                blnValid = False
            End If

            If blnValid Then
                Select Case objBanner.Criteria
                    Case 0 ' AND = cancel the banner when the Impressions expire
                        If objBanner.Impressions < objBanner.Views And objBanner.Impressions <> 0 Then
                            blnValid = False
                        End If
                    Case 1 ' OR = cancel the banner if either the EndDate OR Impressions expire
                        If (objBanner.Impressions < objBanner.Views And objBanner.Impressions <> 0) Or _
                          (Now > objBanner.EndDate And Null.IsNull(objBanner.EndDate) = False) Then
                            blnValid = False
                        End If
                End Select
            End If

            Return blnValid
        End Function

    End Class

End Namespace

