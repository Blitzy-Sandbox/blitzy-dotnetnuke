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

    Public Class ModuleControlController

#Region "Private Members"

        Private Shared provider As DataProvider = DataProvider.Instance()

#End Region

#Region "Private Methods"

        Private Shared Function FillModuleControlCollection(ByVal dr As IDataReader) As ArrayList
            Dim arr As New ArrayList
            Try
                Dim obj As ModuleControlInfo
                While dr.Read
                    ' fill business object
                    obj = FillModuleControlInfo(dr, False)
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

        Private Shared Function FillModuleControlInfo(ByVal dr As IDataReader) As ModuleControlInfo
            Dim moduleControl As ModuleControlInfo

            Try
                moduleControl = FillModuleControlInfo(dr, True)
            Finally
                If Not dr Is Nothing Then
                    dr.Close()
                End If
            End Try

            Return moduleControl
        End Function

        Private Shared Function FillModuleControlInfo(ByVal dr As IDataReader, ByVal CheckForOpenDataReader As Boolean) As ModuleControlInfo
            Dim moduleControl As ModuleControlInfo

            ' read datareader
            Dim canContinue As Boolean = True
            If CheckForOpenDataReader Then
                canContinue = False
                If dr.Read Then
                    canContinue = True
                End If
            End If

            If canContinue Then
                moduleControl = New ModuleControlInfo()
                moduleControl.ModuleControlID = Convert.ToInt32(Null.SetNull(dr("ModuleControlID"), moduleControl.ModuleControlID))
                moduleControl.ModuleDefID = Convert.ToInt32(Null.SetNull(dr("ModuleDefID"), moduleControl.ModuleDefID))
                moduleControl.ControlKey = Convert.ToString(Null.SetNull(dr("ControlKey"), moduleControl.ControlKey))
                moduleControl.ControlTitle = Convert.ToString(Null.SetNull(dr("ControlTitle"), moduleControl.ControlTitle))
                moduleControl.ControlSrc = Convert.ToString(Null.SetNull(dr("ControlSrc"), moduleControl.ControlSrc))
                moduleControl.IconFile = Convert.ToString(Null.SetNull(dr("IconFile"), moduleControl.ControlKey))
                moduleControl.ControlType = CType([Enum].Parse(GetType(SecurityAccessLevel), Convert.ToString(Null.SetNull(dr("ControlType"), moduleControl.ControlType))), SecurityAccessLevel)
                moduleControl.ViewOrder = Convert.ToInt32(Null.SetNull(dr("ViewOrder"), moduleControl.ViewOrder))
                moduleControl.HelpURL = Convert.ToString(Null.SetNull(dr("HelpURL"), moduleControl.HelpURL))
                moduleControl.SupportsPartialRendering = Convert.ToBoolean(Null.SetNull(dr("SupportsPartialRendering"), moduleControl.SupportsPartialRendering))
            Else
                moduleControl = Nothing
            End If

            Return moduleControl
        End Function

#End Region

#Region "Public Shared Methods"

        Public Shared Sub AddModuleControl(ByVal objModuleControl As ModuleControlInfo)
            provider.AddModuleControl(objModuleControl.ModuleDefID, objModuleControl.ControlKey, objModuleControl.ControlTitle, objModuleControl.ControlSrc, objModuleControl.IconFile, CType(objModuleControl.ControlType, Integer), objModuleControl.ViewOrder, objModuleControl.HelpURL, objModuleControl.SupportsPartialRendering)
        End Sub

        Public Shared Sub DeleteModuleControl(ByVal ModuleControlId As Integer)
            provider.DeleteModuleControl(ModuleControlId)
			DataCache.ClearModuleCache()
        End Sub

        Public Shared Function GetModuleControl(ByVal ModuleControlId As Integer) As ModuleControlInfo
            Return FillModuleControlInfo(provider.GetModuleControl(ModuleControlId))
        End Function

        Public Shared Function GetModuleControlByKeyAndSrc(ByVal ModuleDefId As Integer, ByVal ControlKey As String, ByVal ControlSrc As String) As ModuleControlInfo
            Return FillModuleControlInfo(provider.GetModuleControlByKeyAndSrc(ModuleDefId, ControlKey, ControlSrc))
        End Function

        Public Shared Function GetModuleControls(ByVal ModuleDefID As Integer) As ArrayList
            Return FillModuleControlCollection(provider.GetModuleControls(ModuleDefID))
        End Function

        Public Shared Function GetModuleControlsByKey(ByVal ControlKey As String, ByVal ModuleDefId As Integer) As ArrayList
            Return FillModuleControlCollection(provider.GetModuleControlsByKey(ControlKey, ModuleDefId))
        End Function

        Public Shared Sub UpdateModuleControl(ByVal objModuleControl As ModuleControlInfo)
            UpdateModuleControl(objModuleControl, True)
        End Sub

        Public Shared Sub UpdateModuleControl(ByVal objModuleControl As ModuleControlInfo, ByVal clearCache As Boolean)
            provider.UpdateModuleControl(objModuleControl.ModuleControlID, objModuleControl.ModuleDefID, objModuleControl.ControlKey, objModuleControl.ControlTitle, objModuleControl.ControlSrc, objModuleControl.IconFile, CType(objModuleControl.ControlType, Integer), objModuleControl.ViewOrder, objModuleControl.HelpURL, objModuleControl.SupportsPartialRendering)
			If clearCache Then DataCache.ClearModuleCache()
        End Sub
#End Region

    End Class

End Namespace

