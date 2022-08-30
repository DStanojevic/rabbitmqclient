<#
.SYNOPSIS
    Sends GET request to '/messages/workers' endpoint to retrieve all active workers that currently process messages.

.DESCRIPTION
    Call this fuction to obtain all active wokres on specific topic.
#>

function Get-ActiveWorksers{
    $params = @{
        Uri         = $script:global.SubscriberUrl
        Method 		= 'GET'
        Endpoint    = "messages/workers"
    }

    Send-Request $params
}