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
Imports System.Web
Imports System.Collections
Imports System.IO
Imports System.Web.UI

Namespace DotNetNuke.Entities.Host


	Public Class HostSettings
		'Private Shared _Settings As Hashtable

		Public Sub New()
			'_Settings = GetHostSettings()
		End Sub

		Public Shared Function GetHostSetting(ByVal Key As String) As String
			If GetHostSettings.ContainsKey(Key) Then
				Return Convert.ToString(GetHostSettings(Key))
			Else
				Return ""
			End If
		End Function

		Public Shared Function GetHostSettings() As Hashtable
			Dim h As Hashtable
			h = CType(DataCache.GetCache("GetHostSettings"), Hashtable)
			If h Is Nothing Then
                h = New Hashtable
                Dim dr As IDataReader = Nothing
                Try
                    dr = DataProvider.Instance().GetHostSettings
                    While dr.Read()
                        If Not dr.IsDBNull(1) Then
                            h.Add(dr.GetString(0), dr.GetString(1))
                        Else
                            h.Add(dr.GetString(0), "")
                        End If
                    End While
                    DataCache.SetCache("GetHostSettings", h)
                Finally
                    If Not dr Is Nothing Then
                        dr.Close()
                    End If
                End Try
            End If
			Return h
		End Function

		Public Shared Function GetSecureHostSettings() As Hashtable
			Dim h As Hashtable
			h = CType(DataCache.GetCache("GetSecureHostSettings"), Hashtable)
			If h Is Nothing Then
				h = New Hashtable
				Dim SettingName As String
				Dim dr As IDataReader = DataProvider.Instance().GetHostSettings
				While dr.Read()
                    If Not Convert.ToBoolean(dr(2)) Then
                        SettingName = dr.GetString(0)
                        If SettingName.ToLower.IndexOf("password") = -1 Then
                            If Not dr.IsDBNull(1) Then
                                h.Add(SettingName, dr.GetString(1))
                            Else
                                h.Add(SettingName, "")
                            End If
                        End If
                    End If
                End While
				dr.Close()
                DataCache.SetCache("GetSecureHostSettings", h)
			End If
			Return h
		End Function

    End Class
End Namespace
