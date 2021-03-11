import React, { useContext, useState } from "react"
import AccountInfo from "./AccountInfo"
import { UserContext } from "../../UserContext"
import Log from "./Log"

export default function Holder() {
    const status = useContext(UserContext)
    // const [logStat, setStat] = useState(status)
    console.log(status)
    if(status) {
        return(
            <div className="AI" >
            <AccountInfo/>
        </div>
        )
    } else {
        return(
            <div className="AI" >
                <Log props={status} />
            </div>
        )
    }
}