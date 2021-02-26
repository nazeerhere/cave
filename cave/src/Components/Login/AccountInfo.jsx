import React, { useEffect, useState } from "react"
import { Link } from "react-router-dom"

export default function AccountInfo() {
    const [user, setUser] = useState({
        username: "",
        firstname: "",
        lastname: ""
    })

    useEffect(() => {
        fetch("http://localhost:8000/")
            .then(res => res.json())
            .then(res => {
                setUser(res[0])
            })
    }, [])

    console.log(user.ussername)
    return(
        <div>
                   Welcome 
            <Link to="Login" >
                <div className="Username" >
                   {user.ussername}
                </div>
            </Link>
            |
            <Link to="Register" >
                Logout
            </Link>
        </div>
    )
}