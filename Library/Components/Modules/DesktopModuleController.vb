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
Imports System.Globalization
Imports System.IO
Imports System.Xml

Namespace DotNetNuke.Entities.Modules

	Public Class DesktopModuleController

        Public Function AddDesktopModule(ByVal objDesktopModule As DesktopModuleInfo) As Integer
            Return DataProvider.Instance().AddDesktopModule(objDesktopModule.ModuleName, objDesktopModule.FolderName, objDesktopModule.FriendlyName, objDesktopModule.Description, objDesktopModule.Version, objDesktopModule.IsPremium, objDesktopModule.IsAdmin, objDesktopModule.BusinessControllerClass, objDesktopModule.SupportedFeatures, objDesktopModule.CompatibleVersions, objDesktopModule.Dependencies, objDesktopModule.Permissions)
        End Function

        Public Sub AddPortalDesktopModule(ByVal PortalID As Integer, ByVal DesktopModuleID As Integer)
            DataProvider.Instance().AddPortalDesktopModule(PortalID, DesktopModuleID)
        End Sub

        Public Sub DeleteDesktopModule(ByVal DesktopModuleId As Integer)
            DataProvider.Instance().DeleteDesktopModule(DesktopModuleId)
			DataCache.ClearModuleCache()
        End Sub

        Public Sub DeletePortalDesktopModules(ByVal PortalID As Integer, ByVal DesktopModuleID As Integer)
            DataProvider.Instance().DeletePortalDesktopModules(PortalID, DesktopModuleID)
            DataCache.ClearPortalCache(PortalID, True)
        End Sub

        Public Function GetDesktopModule(ByVal DesktopModuleId As Integer) As DesktopModuleInfo
            Return CType(CBO.FillObject(DataProvider.Instance().GetDesktopModule(DesktopModuleId), GetType(DesktopModuleInfo)), DesktopModuleInfo)
        End Function

        Public Function GetDesktopModuleByModuleName(ByVal ModuleName As String) As DesktopModuleInfo
            Return CType(CBO.FillObject(DataProvider.Instance().GetDesktopModuleByModuleName(ModuleName), GetType(DesktopModuleInfo)), DesktopModuleInfo)
        End Function

        Public Function GetDesktopModules() As ArrayList
            Return CBO.FillCollection(DataProvider.Instance().GetDesktopModules(), GetType(DesktopModuleInfo))
        End Function

        Public Function GetDesktopModulesByPortal(ByVal PortalID As Integer) As ArrayList
            Return CBO.FillCollection(DataProvider.Instance().GetDesktopModulesByPortal(PortalID), GetType(DesktopModuleInfo))
        End Function

        Public Function GetPortalDesktopModules(ByVal PortalID As Integer, ByVal DesktopModuleID As Integer) As ArrayList
            Return CBO.FillCollection(DataProvider.Instance().GetPortalDesktopModules(PortalID, DesktopModuleID), GetType(PortalDesktopModuleInfo))
        End Function

        Public Sub UpdateDesktopModule(ByVal objDesktopModule As DesktopModuleInfo)
            UpdateDesktopModule(objDesktopModule, True)
        End Sub

        Friend Sub UpdateDesktopModule(ByVal objDesktopModule As DesktopModuleInfo, ByVal clearCache As Boolean)
            DataProvider.Instance().UpdateDesktopModule(objDesktopModule.DesktopModuleID, objDesktopModule.ModuleName, objDesktopModule.FolderName, objDesktopModule.FriendlyName, objDesktopModule.Description, objDesktopModule.Version, objDesktopModule.IsPremium, objDesktopModule.IsAdmin, objDesktopModule.BusinessControllerClass, objDesktopModule.SupportedFeatures, objDesktopModule.CompatibleVersions, objDesktopModule.Dependencies, objDesktopModule.Permissions)
			If clearCache Then DataCache.ClearModuleCache()
        End Sub

        <Obsolete("As the FriendlyName is not guaranteed to be the same as when the module is created, this method has been replaced by GetDesktopModuleByModuleName(moduleName)")> _
        Public Function GetDesktopModuleByFriendlyName(ByVal FriendlyName As String) As DesktopModuleInfo
            Return CType(CBO.FillObject(DataProvider.Instance().GetDesktopModuleByFriendlyName(FriendlyName), GetType(DesktopModuleInfo)), DesktopModuleInfo)
        End Function

        <Obsolete("As the FriendlyName is not guaranteed to be the same as when the module is created, this method has been replaced by GetDesktopModuleByModuleName(moduleName)")> _
        Public Function GetDesktopModuleByName(ByVal FriendlyName As String) As DesktopModuleInfo
            Return CType(CBO.FillObject(DataProvider.Instance().GetDesktopModuleByFriendlyName(FriendlyName), GetType(DesktopModuleInfo)), DesktopModuleInfo)
        End Function

    End Class

End Namespace

