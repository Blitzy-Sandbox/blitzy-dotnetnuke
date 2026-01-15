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
Imports DotNetNuke.Entities.Modules

Namespace DotNetNuke.Security.Permissions


    Public Class ModulePermissionController

#Region "Public Shared Methods"

        Public Shared Function HasModulePermission(ByVal objModulePermissions As ModulePermissionCollection, ByVal PermissionKey As String) As Boolean
            If Not objModulePermissions Is Nothing Then
                For Each objModulePermission As ModulePermissionInfo In objModulePermissions
                    If objModulePermission.PermissionKey = PermissionKey Then
                        If Null.IsNull(objModulePermission.UserID) Then
                            If PortalSecurity.IsInRoles(objModulePermission.RoleName) Then
                                Return True
                            End If
                        Else
                            If PortalSecurity.IsInRoles("[" & objModulePermission.UserID.ToString & "]") Then
                                Return True
                            End If
                        End If
                    End If
                Next
            End If
            Return False
        End Function

        Public Shared Function HasModulePermission(ByVal moduleID As Integer, ByVal TabId As Integer, ByVal PermissionKey As String) As Boolean
            Dim objModulePermissionController As New ModulePermissionController
            Dim objModulePermissions As ModulePermissionCollection = objModulePermissionController.GetModulePermissionsCollectionByModuleID(moduleID, TabId)
            Return HasModulePermission(objModulePermissions, PermissionKey)
        End Function

#End Region

#Region "Private Methods"

        Private Sub ClearPermissionCache(ByVal moduleId As Integer)
            Dim objModules As New ModuleController
            Dim objModule As ModuleInfo = objModules.GetModule(moduleId, Null.NullInteger)
            DataCache.ClearModulePermissionsCache(objModule.TabID)
        End Sub

        Private Function FillModulePermissionCollection(ByVal dr As IDataReader) As ModulePermissionCollection
            Dim arr As New ModulePermissionCollection()
            Try
                Dim obj As ModulePermissionInfo
                While dr.Read
                    ' fill business object
                    obj = FillModulePermissionInfo(dr, False)
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

        Private Function FillModulePermissionDictionary(ByVal dr As IDataReader) As Dictionary(Of Integer, ModulePermissionCollection)
            Dim dic As New Dictionary(Of Integer, ModulePermissionCollection)
            Try
                Dim obj As ModulePermissionInfo
                While dr.Read
                    ' fill business object
                    obj = FillModulePermissionInfo(dr, False)

                    ' add Module Permission to dictionary
                    If dic.ContainsKey(obj.ModuleID) Then
                        'Add ModulePermission to ModulePermission Collection already in dictionary for TabId
                        dic(obj.ModuleID).Add(obj)
                    Else
                        'Create new ModulePermission Collection for ModuleId
                        Dim collection As New ModulePermissionCollection

                        'Add Permission to Collection
                        collection.Add(obj)

                        'Add Collection to Dictionary
                        dic.Add(obj.ModuleID, collection)
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

        Private Function FillModulePermissionInfo(ByVal dr As IDataReader) As ModulePermissionInfo
            Return FillModulePermissionInfo(dr, True)
        End Function

        Private Function FillModulePermissionInfo(ByVal dr As IDataReader, ByVal CheckForOpenDataReader As Boolean) As ModulePermissionInfo
            Dim permissionInfo As ModulePermissionInfo

            ' read datareader
            Dim canContinue As Boolean = True
            If CheckForOpenDataReader Then
                canContinue = False
                If dr.Read Then
                    canContinue = True
                End If
            End If

            If canContinue Then
                permissionInfo = New ModulePermissionInfo()
                permissionInfo.ModulePermissionID = Convert.ToInt32(Null.SetNull(dr("ModulePermissionID"), permissionInfo.ModulePermissionID))
                permissionInfo.ModuleID = Convert.ToInt32(Null.SetNull(dr("ModuleID"), permissionInfo.ModuleID))
                permissionInfo.ModuleDefID = Convert.ToInt32(Null.SetNull(dr("ModuleDefID"), permissionInfo.ModuleDefID))
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

        Private Function GetModulePermissionsDictionary(ByVal TabId As Integer) As Dictionary(Of Integer, ModulePermissionCollection)

            'Get the Cache Key
            Dim key As String = String.Format(DataCache.ModulePermissionCacheKey, TabId)

            'Try fetching the Dictionary from the Cache
            Dim dicModulePermissions As Dictionary(Of Integer, ModulePermissionCollection) = CType(DataCache.GetPersistentCacheItem(key, GetType(Dictionary(Of Integer, ModulePermissionCollection))), Dictionary(Of Integer, ModulePermissionCollection))

            If dicModulePermissions Is Nothing Then
                'tabPermission caching settings
                Dim timeOut As Int32 = DataCache.TabPermissionCacheTimeOut * Convert.ToInt32(Common.Globals.PerformanceSetting)

                'Get the Dictionary from the database
                dicModulePermissions = FillModulePermissionDictionary(DataProvider.Instance().GetModulePermissionsByTabID(TabId))

                'Cache the Dictionary
                If timeOut > 0 Then
                    DataCache.SetCache(key, dicModulePermissions, TimeSpan.FromMinutes(timeOut), True)
                End If
            End If

            'Return the Dictionary
            Return dicModulePermissions

        End Function

#End Region

#Region "Public Methods"

        Public Function AddModulePermission(ByVal objModulePermission As ModulePermissionInfo) As Integer
            Dim Id As Integer = CType(DataProvider.Instance().AddModulePermission(objModulePermission.ModuleID, objModulePermission.PermissionID, objModulePermission.RoleID, objModulePermission.AllowAccess, objModulePermission.UserID), Integer)
            ClearPermissionCache(objModulePermission.ModuleID)
            Return Id
        End Function

        Public Function AddModulePermission(ByVal objModulePermission As ModulePermissionInfo, ByVal tabId As Integer) As Integer
            Dim Id As Integer = CType(DataProvider.Instance().AddModulePermission(objModulePermission.ModuleID, objModulePermission.PermissionID, objModulePermission.RoleID, objModulePermission.AllowAccess, objModulePermission.UserID), Integer)
            DataCache.ClearModulePermissionsCache(tabId)
            Return Id
        End Function

        Public Sub DeleteModulePermission(ByVal modulePermissionID As Integer)
            DataProvider.Instance().DeleteModulePermission(modulePermissionID)
        End Sub

        Public Sub DeleteModulePermissionsByModuleID(ByVal ModuleID As Integer)
            DataProvider.Instance().DeleteModulePermissionsByModuleID(ModuleID)
            ClearPermissionCache(ModuleID)
        End Sub

        Public Sub DeleteModulePermissionsByUserID(ByVal objUser As UserInfo)
            DataProvider.Instance().DeleteModulePermissionsByUserID(objUser.PortalID, objUser.UserID)
            DataCache.ClearModulePermissionsCachesByPortal(objUser.PortalID)
        End Sub

        Public Function GetModulePermission(ByVal modulePermissionID As Integer) As ModulePermissionInfo
            Dim permission As ModulePermissionInfo

            'Get permission from Database
            Dim dr As IDataReader = DataProvider.Instance().GetModulePermission(modulePermissionID)
            Try
                permission = FillModulePermissionInfo(dr)
            Finally
                If Not dr Is Nothing Then
                    dr.Close()
                End If
            End Try

            Return permission
        End Function

        Public Function GetModulePermissions(ByVal modulePermissions As ModulePermissionCollection, ByVal PermissionKey As String) As String
            Dim strRoles As String = ""
            Dim strUsers As String = ""
            For Each objModulePermission As ModulePermissionInfo In modulePermissions
                If objModulePermission.AllowAccess = True AndAlso objModulePermission.PermissionKey = PermissionKey Then
                    If Null.IsNull(objModulePermission.UserID) Then
                        strRoles += objModulePermission.RoleName + ";"
                    Else
                        strUsers += "[" + objModulePermission.UserID.ToString + "];"
                    End If
                End If
            Next
            Return ";" & strRoles & strUsers
        End Function

        Public Function GetModulePermissionsCollectionByModuleID(ByVal ModuleId As Integer, ByVal TabId As Integer) As ModulePermissionCollection
            Dim bFound As Boolean = False

            'Get the Tab ModulePermission Dictionary
            Dim dicModulePermissions As Dictionary(Of Integer, ModulePermissionCollection) = GetModulePermissionsDictionary(TabId)

            'Get the Collection from the Dictionary
            Dim modulePermissions As ModulePermissionCollection = Nothing
            bFound = dicModulePermissions.TryGetValue(ModuleId, modulePermissions)

            If Not bFound Then
                'try the database
                modulePermissions = FillModulePermissionCollection(DataProvider.Instance().GetModulePermissionsByModuleID(ModuleId, -1))
            End If

            Return modulePermissions
        End Function

        Public Sub UpdateModulePermission(ByVal objModulePermission As ModulePermissionInfo)
            DataProvider.Instance().UpdateModulePermission(objModulePermission.ModulePermissionID, objModulePermission.ModuleID, objModulePermission.PermissionID, objModulePermission.RoleID, objModulePermission.AllowAccess, objModulePermission.UserID)
            ClearPermissionCache(objModulePermission.ModuleID)
        End Sub

#End Region

#Region "Obsolete Methods"

        Private Function FillModulePermissionInfoList(ByVal dr As IDataReader) As ArrayList
            Dim arr As New ArrayList
            Try
                Dim obj As ModulePermissionInfo
                While dr.Read
                    ' fill business object
                    obj = FillModulePermissionInfo(dr, False)
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

        <Obsolete("This method has been deprecated.  This should have been declared as Friend as it was never meant to be used outside of the core.")> _
        Public Function GetModulePermissionsByPortal(ByVal PortalID As Integer) As ArrayList
            Return FillModulePermissionInfoList(DataProvider.Instance().GetModulePermissionsByPortal(PortalID))
        End Function

        <Obsolete("This method has been deprecated.  This should have been declared as Friend as it was never meant to be used outside of the core.")> _
        Public Function GetModulePermissionsByTabID(ByVal TabID As Integer) As ArrayList
            Dim key As String = String.Format(DataCache.ModulePermissionCacheKey, TabID)
            Dim arrModulePermissions As ArrayList = Nothing

            arrModulePermissions = CType(DataCache.GetCache(key), ArrayList)
            If arrModulePermissions Is Nothing Then
                'modulePermission caching settings
                Dim timeOut As Int32 = DataCache.ModulePermissionCacheTimeOut * Convert.ToInt32(Common.Globals.PerformanceSetting)

                arrModulePermissions = FillModulePermissionInfoList(DataProvider.Instance().GetModulePermissionsByTabID(TabID))

                If timeOut <> 0 Then
                    DataCache.SetCache(key, arrModulePermissions, TimeSpan.FromMinutes(timeOut), True)
                End If
            End If
            Return arrModulePermissions
        End Function

        <Obsolete("This method has been deprecated.  GetModulePermissions(ModulePermissionCollection, String) ")> _
        Public Function GetModulePermissionsByModuleID(ByVal objModule As ModuleInfo, ByVal PermissionKey As String) As String
            Dim strRoles As String = ";"
            Dim strUsers As String = ";"
            Dim i As Integer
            For i = 0 To objModule.ModulePermissions.Count - 1
                Dim objModulePermission As Security.Permissions.ModulePermissionInfo = CType(objModule.ModulePermissions(i), Security.Permissions.ModulePermissionInfo)
                If objModulePermission.ModuleID = objModule.ModuleID AndAlso objModulePermission.AllowAccess = True AndAlso objModulePermission.PermissionKey = PermissionKey Then
                    If Null.IsNull(objModulePermission.UserID) Then
                        strRoles += objModulePermission.RoleName + ";"
                    Else
                        strUsers += "[" + objModulePermission.UserID.ToString + "];"
                    End If
                End If
            Next
            Return strRoles + strUsers
        End Function

        <Obsolete("This method has been deprecated.  Please use GetModulePermissionsCollectionByModuleID(ModuleID,TabId)")> _
        Public Function GetModulePermissionsCollectionByModuleID(ByVal moduleID As Integer) As Security.Permissions.ModulePermissionCollection
            Return FillModulePermissionCollection(DataProvider.Instance().GetModulePermissionsByModuleID(moduleID, -1))
        End Function

        <Obsolete("This method has been deprecated.  Please use GetModulePermissionsCollectionByModuleID(ModuleID,TabId)")> _
        Public Function GetModulePermissionsCollectionByModuleID(ByVal arrModulePermissions As ArrayList, ByVal moduleID As Integer) As Security.Permissions.ModulePermissionCollection
            Dim objModulePermissionCollection As New Security.Permissions.ModulePermissionCollection(arrModulePermissions, moduleID)
            Return objModulePermissionCollection
        End Function

        <Obsolete("This method is obsoleted.  It was used to replace lists of RoleIds by lists of RoleNames.")> _
        Public Function GetRoleNamesFromRoleIDs(ByVal Roles As String) As String

            Dim strRoles As String = ""
            If Roles.IndexOf(";") > 0 Then
                Dim arrRoles As String() = Split(Roles, ";")
                Dim i As Integer
                For i = 0 To arrRoles.Length - 1
                    If IsNumeric(arrRoles(i)) Then
                        strRoles += GetRoleName(Convert.ToInt32(arrRoles(i))) + ";"
                    End If
                Next
            ElseIf Roles.Trim.Length > 0 Then
                strRoles = GetRoleName(Convert.ToInt32(Roles.Trim))
            End If
            If Not strRoles.StartsWith(";") Then
                strRoles += ";"
            End If
            Return strRoles
        End Function

        <Obsolete("This method has been deprecated.  Please use HasModulePermission(ModuleID,TabId, PermissionKey)")> _
        Public Shared Function HasModulePermission(ByVal moduleID As Integer, ByVal PermissionKey As String) As Boolean
            Dim objController As New ModulePermissionController
            Dim objModulePermissions As ModulePermissionCollection = objController.FillModulePermissionCollection(DataProvider.Instance().GetModulePermissionsByModuleID(moduleID, -1))
            Return HasModulePermission(objModulePermissions, PermissionKey)
        End Function

#End Region

    End Class



End Namespace
