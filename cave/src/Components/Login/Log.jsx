import React, { useEffect, useState } from "react"
import { Link } from "react-router-dom"


export default function Log({ props, setLoggedIn }) {
    const [signIn, setSign] = useState({
        username: "",
        password: ""
    })
    const [users, setUsers] = useState([])
    // const status = useContext(UserContext)

    useEffect(() => {
        fetch("https://uncool-backend.herokuapp.com/users/")
            .then(res => res.json())
            .then(res => {
                setUsers(res)
            })
    }, [])

    console.log(props)
    // console.log(users[1].id)
    
    const Signin = event => {
        event.preventDefault()

        users.forEach(user => {
            console.log(user)
            if(
                user.ussername === signIn.username &&
                user.password === signIn.password
            ) {
                localStorage.setItem('user-id', JSON.stringify(user.id))
                // document.cookie = `username=${user.username}; expires=` + new Date(9999, 0, 1).toUTCString()
                setLoggedIn(true)
                console.log(props)
            } else {
                console.log("sorry username or password is incorrect")
            }
        })
        setSign({
            username: "",
            password: ""
        })
        document.getElementById("username").value = ""
        document.getElementById("password").value = ""

    }
    const handleChange = event => {
        const {id, value} = event.target
        setSign(prevState => ({
            ...prevState,
            [id] : value
        }))
    }
    console.log(signIn)
    return(
        <div className="Log" >
            {/* <Link to="Login" > */}
                {/* <button className="btn btn-syccess dropdown-toggle" type="button" data-bs-toggle="dropdown" aria-expanded="false" >
                    Login
                </button> */}
            <div className="dropdown-menu dropdown-menu-dark" >
                <form class="px-3 py-3">
                    <div className="mb-3" >
                        <label className="form-label">Username</label>
                        <input  type="text" 
                                id="username"
                                className="form-control" 
                                placeholder="ex. user123" 
                                onChange={handleChange}
                                />

                    </div>
                    <div className="mb-3" >
                        <label className="form-label">Password </label>
                        <input  type="password" 
                                id="password"
                                className="form-control" 
                                placeholder="ex. password123" 
                                onChange={handleChange}
                        />

                    </div>
                    <button type="submit" 
                            class="btn btn-primary"
                            onClick={Signin}
                    >
                        Sign in</button>
                </form>
            </div>    


            {/* </Link> */}
            {/* |
            <Link to="Register" >
                Register
            </Link> */}
        </div>
    )
}