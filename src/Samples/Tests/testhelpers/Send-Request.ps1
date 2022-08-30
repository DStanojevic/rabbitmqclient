function send-request( [HashTable] $Params ) {
    function Get-ErrorResult($err) {
        if ( !$err.Exception.Response ) { return [string]$err }

        if ($PSVersionTable.PSEdition -eq 'Core') {
            $ErrResp = $err.ErrorDetails.Message
        }
        elseif ($PSVersionTable.PSEdition -eq 'Desktop') { # https://stackoverflow.com/a/44000376/82660
            $streamReader = [System.IO.StreamReader]::new($err.Exception.Response.GetResponseStream())
            $ErrResp = $streamReader.ReadToEnd().Trim()
            $streamReader.Close()
        }

        try { $ErrResp = ConvertFrom-Json $ErrResp } catch {}
        if ($ErrResp -eq $null) { $ErrResp = '' }
        $ErrResp
    }

    $p = $Params.Clone()
    if (!$p.Raw) {
        $p.UseBasicParsing = $true

        if (!$p.Uri)                   { $p.Uri = $script:global.Url }
        $p.Uri = $p.Uri + '/' + $p.Endpoint
        if (!$p.Method)                { $p.Method = 'POST' }
        if (!$p.ContentType)            { $p.ContentType = 'application/json; charset=utf-8' }
        if (!$p.Headers)               { $p.Headers = @{} }
        if ($script:global.AccessToken) {
            if (!$p.Headers.Authorization) { $p.Headers.Authorization = 'Bearer ' + $script:global.AccessToken }
        }

        $p.Remove('EndPoint')
    }

    if (!$p.Uri.StartsWith('http')) { throw 'Before calling any module function you need to initialize sessions' }

    if ( $VerbosePreference -eq 'continue' -and $p.Body) {
        "SEND-REQUEST BODY`n" + $p.Body | Write-Verbose
    }

    try {
        $res = Invoke-RestMethod @p
        if ( $VerbosePreference -eq 'continue' -and $res) {
            "SEND-REQUEST RESULT`n" + ($res | ConvertTo-Json -Depth 10) | Write-Verbose
        }
        $res
    } catch {
        $status_code = $_.Exception.Response.StatusCode
        if($status_code -eq 'NotFound'){
            return $null
        }
        if ($PSVersionTable.PSEdition -eq 'Core') {
            $status_desc = $_.Exception.Response.ReasonPhrase
        }
        elseif ($PSVersionTable.PSEdition -eq 'Desktop') {
            $status_desc = $_.Exception.Response.StatusDescription
        }
        Write-Verbose "Status: $status_desc ($status_code)"

        if ( $status_code -ne "Unauthorized" ) { # Unauthorized or access token expired
            Write-Verbose "Non 401 error, getting response and rethrowing"
            $res = Get-ErrorResult $_

            if ( $VerbosePreference -eq 'continue' ) {
                "SEND-REQUEST ERRORS`n" + ($res | ConvertTo-Json -Depth 10) | Write-Verbose
            }

            [array] $custom_err = if ( $res -is [string] ) { $res } else { $res.message + $res.status.message }
            $custom_err += switch ($status_desc) {
                'Bad Request'         { $res.status.code,   'InvalidOperation', $res.additionalInformation }
                'Unprocessable Entity'{ $status_code,       'InvalidData',      $res.errors }
                'Too Many Reqests'    { $status_code,       'LimitsExceeded',   $res.errors }
                'Forbidden'           { $status_code,       'PermissionDenied', $null }
                default               { $status_code,       'NotSpecified',     $res.additionalInformation }
            }

            # Arguments to ErrorRecord constructor (message, errorId, errorCategory, targetObject): https://goo.gl/NTG97M
            $e = new-object System.Management.Automation.ErrorRecord $custom_err
            throw $e
        }

        if ($script:global.AccessToken) {
            Write-Verbose "401 Returned, trying to refresh token"
            Initialize-Session -Password $script:global.RefreshToken -Username $null -Url $script:global.Url
            $p.Headers.Authorization = 'Bearer ' + $script:global.AccessToken
            Invoke-RestMethod @p
        }
    }
}
