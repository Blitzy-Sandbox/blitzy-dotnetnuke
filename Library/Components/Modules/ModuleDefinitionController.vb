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

Namespace DotNetNuke.Entities.Modules.Definitions

	Public Class ModuleDefinitionController

        Public Function AddModuleDefinition(ByVal objModuleDefinition As ModuleDefinitionInfo) As Integer
            Return DataProvider.Instance().AddModuleDefinition(objModuleDefinition.DesktopModuleID, objModuleDefinition.FriendlyName, objModuleDefinition.DefaultCacheTime)
        End Function

        Public Sub DeleteModuleDefinition(ByVal ModuleDefinitionId As Integer)
            DataProvider.Instance().DeleteModuleDefinition(ModuleDefinitionId)
			DataCache.ClearModuleCache()
        End Sub

        Public Function GetModuleDefinition(ByVal ModuleDefId As Integer) As ModuleDefinitionInfo
            Return CType(CBO.FillObject(DataProvider.Instance().GetModuleDefinition(ModuleDefId), GetType(ModuleDefinitionInfo)), ModuleDefinitionInfo)
        End Function

		Public Function GetModuleDefinitionByName(ByVal DesktopModuleId As Integer, ByVal FriendlyName As String) As ModuleDefinitionInfo
            Return CType(CBO.FillObject(DataProvider.Instance().GetModuleDefinitionByName(DesktopModuleId, FriendlyName), GetType(ModuleDefinitionInfo)), ModuleDefinitionInfo)
        End Function

        Public Function GetModuleDefinitions(ByVal DesktopModuleId As Integer) As ArrayList
            Return CBO.FillCollection(DataProvider.Instance().GetModuleDefinitions(DesktopModuleId), GetType(ModuleDefinitionInfo))
        End Function

        Public Sub UpdateModuleDefinition(ByVal objModuleDefinition As ModuleDefinitionInfo)
            UpdateModuleDefinition(objModuleDefinition, True)
        End Sub

        Public Sub UpdateModuleDefinition(ByVal objModuleDefinition As ModuleDefinitionInfo, ByVal clearCache As Boolean)
            DataProvider.Instance().UpdateModuleDefinition(objModuleDefinition.ModuleDefID, objModuleDefinition.FriendlyName, objModuleDefinition.DefaultCacheTime)
			If clearCache Then DataCache.ClearModuleCache()
        End Sub
    End Class

End Namespace

