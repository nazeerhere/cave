import React from "react"
import { Link } from "react-router-dom"


export default function Log() {

    return(
        <div className="Log" >
            <Link to="Login" >
                <div className="Username" >
                   Login
                </div>
            </Link>
            |
            <Link to="Register" >
                Register
            </Link>
        </div>
    )
}