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
Imports System.Data
Imports DotNetNuke

Namespace DotNetNuke.Entities.Portals

    Public Class PortalAliasController

        Public Function AddPortalAlias(ByVal objPortalAliasInfo As PortalAliasInfo) As Integer
            DataCache.ClearHostCache(False)

            Return DataProvider.Instance().AddPortalAlias(objPortalAliasInfo.PortalID, objPortalAliasInfo.HTTPAlias.ToLower)
        End Function

        Public Sub DeletePortalAlias(ByVal PortalAliasID As Integer)
            DataCache.ClearHostCache(False)

            DataProvider.Instance().DeletePortalAlias(PortalAliasID)
        End Sub

        Public Function GetPortalAlias(ByVal PortalAlias As String, ByVal PortalID As Integer) As PortalAliasInfo
            Return CType(CBO.FillObject(DataProvider.Instance().GetPortalAlias(PortalAlias, PortalID), GetType(PortalAliasInfo)), PortalAliasInfo)
        End Function

        Public Function GetPortalAliasArrayByPortalID(ByVal PortalID As Integer) As ArrayList
            Dim dr As IDataReader = DataProvider.Instance().GetPortalAliasByPortalID(PortalID)
            Try
                Dim arr As New ArrayList
                While dr.Read
                    Dim objPortalAliasInfo As New PortalAliasInfo
                    objPortalAliasInfo.PortalAliasID = Convert.ToInt32(dr("PortalAliasID"))
                    objPortalAliasInfo.PortalID = Convert.ToInt32(dr("PortalID"))
                    objPortalAliasInfo.HTTPAlias = Convert.ToString(dr("HTTPAlias")).ToLower
                    arr.Add(objPortalAliasInfo)
                End While
                Return arr
            Finally
                If Not dr Is Nothing Then
                    dr.Close()
                End If
            End Try
        End Function

        Public Function GetPortalAliasByPortalAliasID(ByVal PortalAliasID As Integer) As PortalAliasInfo
            Return CType(CBO.FillObject((DataProvider.Instance().GetPortalAliasByPortalAliasID(PortalAliasID)), GetType(PortalAliasInfo)), PortalAliasInfo)
        End Function

        Public Function GetPortalAliasByPortalID(ByVal PortalID As Integer) As PortalAliasCollection
            Dim dr As IDataReader = DataProvider.Instance().GetPortalAliasByPortalID(PortalID)
            Try
                Dim objPortalAliasCollection As New PortalAliasCollection
                While dr.Read
                    Dim objPortalAliasInfo As New PortalAliasInfo
                    objPortalAliasInfo.PortalAliasID = Convert.ToInt32(dr("PortalAliasID"))
                    objPortalAliasInfo.PortalID = Convert.ToInt32(dr("PortalID"))
                    objPortalAliasInfo.HTTPAlias = Convert.ToString(dr("HTTPAlias"))
                    objPortalAliasCollection.Add(Convert.ToString(dr("HTTPAlias")).ToLower, objPortalAliasInfo)
                End While
                Return objPortalAliasCollection
            Finally
                If Not dr Is Nothing Then
                    dr.Close()
                End If
            End Try
        End Function

        Public Function GetPortalAliases() As PortalAliasCollection
            Return GetPortalAliasByPortalID(-1)
        End Function

        Public Function GetPortalByPortalAliasID(ByVal PortalAliasId As Integer) As PortalInfo
            Return CType(CBO.FillObject(DataProvider.Instance().GetPortalByPortalAliasID(PortalAliasId), GetType(PortalInfo)), PortalInfo)
        End Function

        Public Sub UpdatePortalAliasInfo(ByVal objPortalAliasInfo As PortalAliasInfo)
            DataCache.ClearHostCache(False)

            DataProvider.Instance().UpdatePortalAliasInfo(objPortalAliasInfo.PortalAliasID, objPortalAliasInfo.PortalID, objPortalAliasInfo.HTTPAlias.ToLower)
        End Sub

    End Class

End Namespace
