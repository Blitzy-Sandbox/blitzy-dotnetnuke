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
Imports System.Collections.Generic
Imports System.Data
Imports DotNetNuke

Namespace DotNetNuke.Security.Permissions

    Public Class FolderPermissionController

#Region "Public Shared Methods"

        Public Shared Function HasFolderPermission(ByVal objFolderPermissions As FolderPermissionCollection, ByVal PermissionKey As String) As Boolean
            For Each objFolderPermission As FolderPermissionInfo In objFolderPermissions
                If objFolderPermission.PermissionKey = PermissionKey Then
                    If Null.IsNull(objFolderPermission.UserID) Then
                        If PortalSecurity.IsInRoles(objFolderPermission.RoleName) Then
                            Return True
                        End If
                    Else
                        If PortalSecurity.IsInRoles("[" & objFolderPermission.UserID.ToString & "]") Then
                            Return True
                        End If
                    End If
                End If
            Next
            Return False
        End Function

        Public Shared Function HasFolderPermission(ByVal PortalID As Integer, ByVal FolderPath As String, ByVal PermissionKey As String) As Boolean
            'superuser automatically has all folder permissions - this is also necessary as no folder permissions are defined for host portal folder
            If UserController.GetCurrentUserInfo.IsSuperUser = True Then
                Return True
            End If
            Dim objFolderPermissionController As New FolderPermissionController
            Dim objFolderPermissions As FolderPermissionCollection = objFolderPermissionController.GetFolderPermissionsCollectionByFolderPath(PortalID, FolderPath)
            Return HasFolderPermission(objFolderPermissions, PermissionKey)
        End Function

#End Region

#Region "Private Members"

        Private Sub ClearPermissionCache(ByVal PortalID As Integer)
            DataCache.ClearFolderPermissionsCache(PortalID)
        End Sub

        Private Function FillFolderPermissionCollection(ByVal dr As IDataReader) As FolderPermissionCollection
            Dim arr As New FolderPermissionCollection()
            Try
                Dim obj As FolderPermissionInfo
                While dr.Read
                    ' fill business object
                    obj = FillFolderPermissionInfo(dr, False)
                    ' add to collection
                    arr.Add(obj)
                End While
            Catch exc As Exception
                LogException(exc)
            Finally
                ' close datareader
                If Not dr Is Nothing Then
                    dr.Close()
                End If
            End Try
            Return arr
        End Function

        Private Function FillFolderPermissionDictionary(ByVal dr As IDataReader) As Dictionary(Of String, FolderPermissionCollection)
            Dim dic As New Dictionary(Of String, FolderPermissionCollection)
            Try
                Dim obj As FolderPermissionInfo
                While dr.Read
                    ' fill business object
                    obj = FillFolderPermissionInfo(dr, False)

                    ' add Folder Permission to dictionary
                    If dic.ContainsKey(obj.FolderPath) Then
                        'Add FolderPermission to FolderPermission Collection already in dictionary for FolderPath
                        dic(obj.FolderPath).Add(obj)
                    Else
                        'Create new FolderPermission Collection for TabId
                        Dim collection As New FolderPermissionCollection

                        'Add Permission to Collection
                        collection.Add(obj)

                        'Add Collection to Dictionary
                        dic.Add(obj.FolderPath, collection)
                    End If
                End While
            Catch exc As Exception
                LogException(exc)
            Finally
                ' close datareader
                If Not dr Is Nothing Then
                    dr.Close()
                End If
            End Try
            Return dic
        End Function

        Private Function FillFolderPermissionInfoList(ByVal dr As IDataReader) As ArrayList
            Dim arr As New ArrayList
            Try
                Dim obj As FolderPermissionInfo
                While dr.Read
                    ' fill business object
                    obj = FillFolderPermissionInfo(dr, False)
                    ' add to collection
                    arr.Add(obj)
                End While
            Catch exc As Exception
                LogException(exc)
            Finally
                ' close datareader
                If Not dr Is Nothing Then
                    dr.Close()
                End If
            End Try
            Return arr
        End Function

        Private Function FillFolderPermissionInfo(ByVal dr As IDataReader) As FolderPermissionInfo
            Return FillFolderPermissionInfo(dr, True)
        End Function

        Private Function FillFolderPermissionInfo(ByVal dr As IDataReader, ByVal CheckForOpenDataReader As Boolean) As FolderPermissionInfo
            Dim permissionInfo As FolderPermissionInfo

            ' read datareader
            Dim canContinue As Boolean = True
            If CheckForOpenDataReader Then
                canContinue = False
                If dr.Read Then
                    canContinue = True
                End If
            End If

            If canContinue Then
                permissionInfo = New FolderPermissionInfo()
                permissionInfo.FolderPermissionID = Convert.ToInt32(Null.SetNull(dr("FolderPermissionID"), permissionInfo.FolderPermissionID))
                permissionInfo.FolderID = Convert.ToInt32(Null.SetNull(dr("FolderID"), permissionInfo.FolderID))
                permissionInfo.FolderPath = Convert.ToString(Null.SetNull(dr("FolderPath"), permissionInfo.FolderPath))
                permissionInfo.PermissionID = Convert.ToInt32(Null.SetNull(dr("PermissionID"), permissionInfo.PermissionID))
                permissionInfo.UserID = Convert.ToInt32(Null.SetNull(dr("UserID"), permissionInfo.UserID))
                permissionInfo.Username = Convert.ToString(Null.SetNull(dr("Username"), permissionInfo.Username))
                permissionInfo.DisplayName = Convert.ToString(Null.SetNull(dr("DisplayName"), permissionInfo.DisplayName))
                If permissionInfo.UserID = Null.NullInteger Then
                    permissionInfo.RoleID = Convert.ToInt32(Null.SetNull(dr("RoleID"), permissionInfo.RoleID))
                    permissionInfo.RoleName = Convert.ToString(Null.SetNull(dr("RoleName"), permissionInfo.RoleName))
                Else
                    permissionInfo.RoleID = Integer.Parse(glbRoleNothing)
                    permissionInfo.RoleName = ""
                End If
                permissionInfo.AllowAccess = Convert.ToBoolean(Null.SetNull(dr("AllowAccess"), permissionInfo.AllowAccess))
                permissionInfo.PermissionCode = Convert.ToString(Null.SetNull(dr("PermissionCode"), permissionInfo.PermissionCode))
                permissionInfo.PermissionKey = Convert.ToString(Null.SetNull(dr("PermissionKey"), permissionInfo.PermissionKey))
                permissionInfo.PermissionName = Convert.ToString(Null.SetNull(dr("PermissionName"), permissionInfo.PermissionName))
            Else
                permissionInfo = Nothing
            End If

                Return permissionInfo

        End Function

        Private Function GetFolderPermissionsDictionary(ByVal PortalID As Integer) As Dictionary(Of String, FolderPermissionCollection)

            'Get the Cache Key
            Dim key As String = String.Format(DataCache.FolderPermissionCacheKey, PortalID)

            'Try fetching the Dictionary from the Cache
            Dim dicFolderPermissions As Dictionary(Of String, FolderPermissionCollection) = CType(DataCache.GetCache(key), Dictionary(Of String, FolderPermissionCollection))

            If dicFolderPermissions Is Nothing Then
                'tabPermission caching settings
                Dim timeOut As Int32 = DataCache.FolderPermissionCacheTimeOut * Convert.ToInt32(Common.Globals.PerformanceSetting)

                'Get the Dictionary from the database
                dicFolderPermissions = FillFolderPermissionDictionary(DataProvider.Instance().GetFolderPermissionsByPortal(PortalID))

                'Cache the Dictionary
                If timeOut > 0 Then
                    DataCache.SetCache(key, dicFolderPermissions, TimeSpan.FromMinutes(timeOut), True)
                End If
            End If

            'Return the Dictionary
            Return dicFolderPermissions

        End Function

#End Region

        Public Function AddFolderPermission(ByVal objFolderPermission As FolderPermissionInfo) As Integer
            ClearPermissionCache(objFolderPermission.PortalID)
            Return CType(DataProvider.Instance().AddFolderPermission(objFolderPermission.FolderID, objFolderPermission.PermissionID, objFolderPermission.RoleID, objFolderPermission.AllowAccess, objFolderPermission.UserID), Integer)
        End Function

        Public Sub DeleteFolderPermission(ByVal FolderPermissionID As Integer)
            Dim objFolderPermission As FolderPermissionInfo = GetFolderPermission(FolderPermissionID)

            DataProvider.Instance().DeleteFolderPermission(FolderPermissionID)

            ClearPermissionCache(objFolderPermission.PortalID)
        End Sub

        Public Sub DeleteFolderPermissionsByFolder(ByVal PortalID As Integer, ByVal FolderPath As String)
            DataProvider.Instance().DeleteFolderPermissionsByFolderPath(PortalID, FolderPath)
            ClearPermissionCache(PortalID)
        End Sub

        Public Sub DeleteFolderPermissionsByUserID(ByVal objUser As UserInfo)
            DataProvider.Instance().DeleteFolderPermissionsByUserID(objUser.PortalID, objUser.UserID)
            ClearPermissionCache(objUser.PortalID)
        End Sub

        Public Function GetFolderPermission(ByVal FolderPermissionID As Integer) As FolderPermissionInfo
            Dim permission As FolderPermissionInfo

            'Get permission from Database
            Dim dr As IDataReader = DataProvider.Instance().GetFolderPermission(FolderPermissionID)
            Try
                permission = FillFolderPermissionInfo(dr)
            Finally
                If Not dr Is Nothing Then
                    dr.Close()
                End If
            End Try

            Return permission
        End Function

        Public Function GetFolderPermissionsCollectionByFolderPath(ByVal PortalID As Integer, ByVal Folder As String) As FolderPermissionCollection
            Dim bFound As Boolean = False

            'Get the Portal FolderPermission Dictionary
            Dim dicFolderPermissions As Dictionary(Of String, FolderPermissionCollection) = GetFolderPermissionsDictionary(PortalID)

            'Get the Collection from the Dictionary
            Dim folderPermissions As FolderPermissionCollection = Nothing
            bFound = dicFolderPermissions.TryGetValue(Folder, folderPermissions)

            If Not bFound Then
                'try the database
                folderPermissions = FillFolderPermissionCollection(DataProvider.Instance().GetFolderPermissionsByFolderPath(PortalID, Folder, -1))
            End If

            Return folderPermissions
        End Function

        Public Function GetFolderPermissionsByFolderPath(ByVal FolderPermissions As ArrayList, ByVal FolderPath As String, ByVal PermissionKey As String) As String
            Dim strRoles As String = ""
            Dim strUsers As String = ""
            For Each objFolderPermission As FolderPermissionInfo In FolderPermissions
                If objFolderPermission.FolderPath = FolderPath AndAlso objFolderPermission.AllowAccess = True AndAlso objFolderPermission.PermissionKey = PermissionKey Then
                    If Null.IsNull(objFolderPermission.UserID) Then
                        strRoles += objFolderPermission.RoleName + ";"
                    Else
                        strUsers += "[" + objFolderPermission.UserID.ToString + "];"
                    End If
                End If
            Next
            Return ";" & strRoles & strUsers
        End Function

        Public Sub UpdateFolderPermission(ByVal objFolderPermission As FolderPermissionInfo)
            DataProvider.Instance().UpdateFolderPermission(objFolderPermission.FolderPermissionID, objFolderPermission.FolderID, objFolderPermission.PermissionID, objFolderPermission.RoleID, objFolderPermission.AllowAccess, objFolderPermission.UserID)
            ClearPermissionCache(objFolderPermission.PortalID)
        End Sub


#Region "Obsolete Methods"

        <Obsolete("This method has been deprecated.  Please use GetFolderPermissionsCollectionByFolderPath(PortalId, Folder)")> _
        Public Function GetFolderPermissionsByFolder(ByVal PortalID As Integer, ByVal Folder As String) As ArrayList
            Return FillFolderPermissionInfoList(DataProvider.Instance().GetFolderPermissionsByFolderPath(PortalID, Folder, -1))
        End Function

        <Obsolete("This method has been deprecated.  Please use GetFolderPermissionsCollectionByFolderPath(PortalId, Folder)")> _
        Public Function GetFolderPermissionsByFolder(ByVal arrFolderPermissions As ArrayList, ByVal FolderPath As String) As Security.Permissions.FolderPermissionCollection
            Dim p As New Security.Permissions.FolderPermissionCollection

            Dim i As Integer
            For i = 0 To arrFolderPermissions.Count - 1
                Dim objFolderPermission As Security.Permissions.FolderPermissionInfo = CType(arrFolderPermissions(i), Security.Permissions.FolderPermissionInfo)
                If objFolderPermission.FolderPath = FolderPath Then
                    p.Add(objFolderPermission)
                End If
            Next
            Return p
        End Function

        <Obsolete("This method has been deprecated.  Please use GetFolderPermissionsCollectionByFolderPath(PortalId, Folder)")> _
        Public Function GetFolderPermissionsCollectionByFolderPath(ByVal arrFolderPermissions As ArrayList, ByVal FolderPath As String) As Security.Permissions.FolderPermissionCollection
            Dim objFolderPermissionCollection As New Security.Permissions.FolderPermissionCollection(arrFolderPermissions, FolderPath)
            Return objFolderPermissionCollection
        End Function

#End Region


    End Class

End Namespace
