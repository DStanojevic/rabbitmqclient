<#
.SYNOPSIS
    Create new user object.

.DESCRIPTION
    This function will create new offline user.
    Use it with Register-User or Update-User.
#>
function New-SendMessageRequest {
    [CmdletBinding()]
    param(
            [Parameter(Mandatory = $true,   ValueFromPipelineByPropertyName = $true)] [string]      $TopicName,
            [Parameter(Mandatory = $false,  ValueFromPipelineByPropertyName = $true)] [string]      $MessageId,
            [Parameter(Mandatory = $false,  ValueFromPipelineByPropertyName = $true)] [datetime]    $MessageTime,
			[Parameter(Mandatory = $false,   ValueFromPipelineByPropertyName = $true)] [string]      $MessageBody,
            [Parameter(Mandatory = $false,   ValueFromPipelineByPropertyName = $true)] [string]      $MessageBodyType
    )

    $SendMessageRequest = @{
        Header = @{
            TopicName = $TopicName;
            MessageId = if($MessageId) {$MessageId} else {[guid]::NewGuid().Guid};
            MessageTime = if($MessageTime){$MessageTime} else {Get-Date};
        };
        Body = if($MessageBody) {$MessageBody} else {Get-RandomString};
    }
    if($MessageBodyType)
        {$SendMessageRequest.BodyTypeName = $MessageBodyType}

    $SendMessageRequest
}